// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Linq;
using Content.Server._NF.Bank;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Server side of the flying-craft purchase consoles (civilian shuttles / security shuttles / boards). Lists a
/// catalog of prototypes with prices, takes the buyer's bank money, and spawns the purchase — craft consoles onto a
/// chosen nearby map spawner of the matching kind (within range), the board console at the console itself.
/// </summary>
public sealed class FlyingCraftPurchaseConsoleSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftPurchaseConsoleComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<FlyingCraftPurchaseConsoleComponent, FlyingCraftBuyMessage>(OnBuy);
    }

    private void OnOpened(Entity<FlyingCraftPurchaseConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshState(ent, args.Actor);
    }

    private void OnBuy(Entity<FlyingCraftPurchaseConsoleComponent> ent, ref FlyingCraftBuyMessage args)
    {
        var actor = args.Actor;
        var protoId = args.Proto; // can't use the ref arg inside the lambda
        var entry = ent.Comp.Catalog.FirstOrDefault(e => e.Proto.Id == protoId);
        if (entry == null)
            return;

        // Resolve where the purchase spawns.
        EntityCoordinates coords;
        if (ent.Comp.SpawnerKind == FlyingCraftSpawnerKind.None)
        {
            coords = Transform(ent).Coordinates;
        }
        else
        {
            if (args.Spawner is not { } netSpawner)
            {
                _popup.PopupEntity(Loc.GetString("flying-craft-console-pick-spawn"), ent, actor);
                return;
            }

            var spawner = GetEntity(netSpawner);
            if (!IsValidSpawner(ent, spawner))
            {
                _popup.PopupEntity(Loc.GetString("flying-craft-console-bad-spawn"), ent, actor);
                return;
            }

            coords = Transform(spawner).Coordinates;
        }

        if (!_bank.TryGetBalance(actor, out var balance) || balance < entry.Price)
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-console-no-money"), ent, actor);
            return;
        }

        if (!_bank.TryBankWithdraw(actor, entry.Price))
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-console-no-money"), ent, actor);
            return;
        }

        Spawn(entry.Proto, coords);
        _popup.PopupEntity(Loc.GetString("flying-craft-console-purchased"), ent, actor);
        RefreshState(ent, actor);
    }

    private bool IsValidSpawner(Entity<FlyingCraftPurchaseConsoleComponent> console, EntityUid spawner)
    {
        var ok = console.Comp.SpawnerKind switch
        {
            FlyingCraftSpawnerKind.Civilian => HasComp<FlyingCraftCivilianSpawnerComponent>(spawner),
            FlyingCraftSpawnerKind.Security => HasComp<FlyingCraftSecuritySpawnerComponent>(spawner),
            _ => false,
        };
        if (!ok)
            return false;

        var a = _xform.GetMapCoordinates(spawner);
        var b = _xform.GetMapCoordinates(console.Owner);
        return a.MapId == b.MapId && (a.Position - b.Position).Length() <= console.Comp.SpawnRange;
    }

    private List<EntityUid> GetSpawners(Entity<FlyingCraftPurchaseConsoleComponent> console)
    {
        var results = new List<EntityUid>();
        var coords = _xform.GetMapCoordinates(console.Owner);

        switch (console.Comp.SpawnerKind)
        {
            case FlyingCraftSpawnerKind.Civilian:
                var civ = new HashSet<Entity<FlyingCraftCivilianSpawnerComponent>>();
                _lookup.GetEntitiesInRange(coords, console.Comp.SpawnRange, civ);
                foreach (var s in civ)
                    results.Add(s.Owner);
                break;
            case FlyingCraftSpawnerKind.Security:
                var sec = new HashSet<Entity<FlyingCraftSecuritySpawnerComponent>>();
                _lookup.GetEntitiesInRange(coords, console.Comp.SpawnRange, sec);
                foreach (var s in sec)
                    results.Add(s.Owner);
                break;
        }

        return results;
    }

    private void RefreshState(Entity<FlyingCraftPurchaseConsoleComponent> ent, EntityUid actor)
    {
        _bank.TryGetBalance(actor, out var balance);

        var catalog = ent.Comp.Catalog
            .Select(e => new FlyingCraftCatalogEntry(e.Proto.Id, GetName(e.Proto), e.Price))
            .ToList();

        var needsSpawner = ent.Comp.SpawnerKind != FlyingCraftSpawnerKind.None;
        var spawners = new List<FlyingCraftSpawnPoint>();
        if (needsSpawner)
        {
            foreach (var spawner in GetSpawners(ent))
                spawners.Add(new FlyingCraftSpawnPoint(GetNetEntity(spawner), Name(spawner)));
        }

        _ui.SetUiState(ent.Owner, FlyingCraftConsoleUiKey.Key,
            new FlyingCraftConsoleState(balance, needsSpawner, catalog, spawners));
    }

    private string GetName(EntProtoId proto)
        => _proto.TryIndex(proto, out var p) ? p.Name : proto.Id;
}
