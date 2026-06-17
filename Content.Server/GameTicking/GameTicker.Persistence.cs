using System;
using Content.Server._Lua.Sectors;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.EntitySerialization;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking;

// Lua/Frontier persistence (session-save): additive save/restore of secondary Lua sector maps (and the
// shuttles + stations on them) alongside the existing single-DefaultMap persistence. The main sector
// (DefaultMap) is saved/restored by the unchanged GameMapManager path; this file only adds the extra
// sector maps so player shuttles parked in those sectors survive a server restart too.
public sealed partial class GameTicker
{
    private static ResPath SectorSavePath(string savePath) => new(savePath + ".sectors");

    /// <summary>
    /// Saves every persistent Lua sector map (those carrying <see cref="PersistentSectorMapComponent"/>)
    /// plus the nullspace station meta-entities whose grids live on them, into one FileCategory.Save file
    /// next to the main world save. Called from SaveMaps after the DefaultMap save. Shuttles parked/docked
    /// on a sector map ride along automatically. The main DefaultMap is not marked, so it is excluded here.
    /// </summary>
    private void SaveSectorMaps(string savePath)
    {
        var sectorMapUids = new HashSet<EntityUid>();
        var sectorMapIds = new HashSet<MapId>();
        foreach (var mapId in _map.GetAllMapIds())
        {
            if (!_map.TryGetMap(mapId, out var mapUid))
                continue;
            if (!HasComp<PersistentSectorMapComponent>(mapUid.Value))
                continue;
            sectorMapUids.Add(mapUid.Value);
            sectorMapIds.Add(mapId);
        }

        if (sectorMapUids.Count == 0)
            return;

        // Include the (nullspace) station entities for those maps so their stations restore already linked
        // to their grids — StationDataComponent.Grids is a serialized DataField, so the cross-references are
        // remapped on load. This is what keeps POI/sector stations working after a restore.
        var saveSet = new HashSet<EntityUid>(sectorMapUids);
        var stationQuery = AllEntityQuery<StationDataComponent>();
        while (stationQuery.MoveNext(out var stationUid, out var data))
        {
            foreach (var grid in data.Grids)
            {
                if (TryComp<TransformComponent>(grid, out var gridXform) && sectorMapIds.Contains(gridXform.MapID))
                {
                    saveSet.Add(stationUid);
                    break;
                }
            }
        }

        var pausedMaps = new List<MapId>();
        try
        {
            foreach (var mapId in sectorMapIds)
            {
                _map.SetPaused(mapId, true);
                pausedMaps.Add(mapId);
            }

            var ok = _loader.TrySaveGeneric(
                saveSet,
                SectorSavePath(savePath),
                out var category,
                new SerializationOptions { Category = FileCategory.Save });

            _adminLogger.Add(LogType.EventRan, LogImpact.High,
                $"SECTOR SAVE STATUS: {ok} CATEGORY: {category} MAPS: {sectorMapUids.Count} STATIONS: {saveSet.Count - sectorMapUids.Count}");
        }
        finally
        {
            foreach (var mapId in pausedMaps)
                _map.SetPaused(mapId, false);
        }
    }

    /// <summary>
    /// Restores the persistent Lua sector maps saved by <see cref="SaveSectorMaps"/>. Runs at the end of
    /// LoadMaps (after DefaultMap is set) only when game.usepersistence is on and the sibling save exists.
    /// Loads every saved sector map (with its grids, shuttles and station entities), re-registers each into
    /// <see cref="SectorSystem"/> so Starmap/FTL-by-id resolve, and initializes the map. EnsureSector's
    /// ContainsKey guard then skips re-creating these sectors at RoundStarting, so nothing is duplicated.
    /// Any failure is logged and the round simply continues with freshly-generated sectors.
    /// </summary>
    private void RestoreSectors()
    {
        if (!_cfg.GetCVar(CCVars.UsePersistence))
            return;

        var savePath = _cfg.GetCVar(CCVars.GameMap);
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        var path = SectorSavePath(savePath);
        if (!_resourceManager.UserData.Exists(path))
            return;

        try
        {
            if (!_loader.TryLoadGeneric(
                    path,
                    out var maps,
                    out _,
                    new MapLoadOptions
                    {
                        ExpectedCategory = FileCategory.Save,
                        DeserializationOptions = new DeserializationOptions
                        {
                            InitializeMaps = false,
                            PauseMaps = false,
                        },
                    })
                || maps == null)
            {
                Log.Error("[Persistence] Sector restore: TryLoadGeneric failed; sectors will be generated fresh.");
                return;
            }

            var sectors = EntityManager.System<SectorSystem>();
            var restored = 0;
            foreach (var map in maps)
            {
                if (!TryComp<PersistentSectorMapComponent>(map.Owner, out var marker) || string.IsNullOrEmpty(marker.ConfigId))
                    continue;

                if (!sectors.RestoreSectorInstance(marker.ConfigId, map.Comp.MapId, map.Owner))
                    continue;

                if (!_map.IsInitialized(map.Comp.MapId))
                    _map.InitializeMap(map.Comp.MapId);
                restored++;
            }

            _adminLogger.Add(LogType.EventRan, LogImpact.High,
                $"SECTOR RESTORE: loaded {maps.Count} map(s), restored {restored} sector(s).");
        }
        catch (Exception e)
        {
            Log.Error($"[Persistence] Sector restore threw; sectors will be generated fresh:\n{e}");
        }
    }
}
