// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

namespace Content.Server._Lua.Sectors;

/// <summary>
/// Lua persistence (session-save): SAVED marker placed on every secondary Lua sector map created by
/// <see cref="SectorSystem"/>. Whole-world persistence collects exactly the maps carrying this component
/// into a sibling FileCategory.Save file and re-identifies each one (by <see cref="ConfigId"/>) after a
/// generic load assigns fresh map ids. The main station map (GameTicker.DefaultMap) is intentionally NOT
/// marked — it is saved/restored by the existing single-map persistence path.
/// </summary>
[RegisterComponent]
public sealed partial class PersistentSectorMapComponent : Component
{
    /// <summary>
    /// The <see cref="Content.Shared._Lua.Sectors.SectorSystemPrototype"/> id this map was generated from,
    /// used to re-register the restored map into SectorSystem on load.
    /// </summary>
    [DataField]
    public string ConfigId = string.Empty;
}
