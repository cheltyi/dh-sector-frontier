// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Applies per-tier stats to a flying craft (top speed from <see cref="FlyingCraftComponent.TierSpeeds"/>, hull
/// durability = BaseDurability × 1.3^(tier-1), and the FTL jump component at tier 3+) on spawn, and handles the
/// craft's board slot: an upgrade board raises the tier by one (combat craft), a weapon board converts a civilian
/// into a combat class (copying that class's stats + weapons, WITHOUT ejecting the seated pilot).
/// </summary>
public sealed class FlyingCraftTierSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly FlyingCraftWeaponSystem _weapons = default!;

    public const string BoardSlotId = "flyingcraft-board";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftComponent, MapInitEvent>(OnMapInit);
        // Board insertion is subscribed on the dedicated slot marker — the FlyingCraft + EntInserted pair is already
        // taken by FlyingCraftPilotSystem and the engine forbids a second directed subscription on the same pair.
        SubscribeLocalEvent<FlyingCraftBoardSlotComponent, EntInsertedIntoContainerMessage>(OnBoardInserted);
    }

    private void OnMapInit(Entity<FlyingCraftComponent> ent, ref MapInitEvent args)
    {
        ApplyTier(ent, ent.Comp.Tier);
    }

    /// <summary>Sets the craft's speed, durability and FTL capability for the given tier.</summary>
    public void ApplyTier(Entity<FlyingCraftComponent> ent, int tier)
    {
        tier = Math.Clamp(tier, 1, ent.Comp.MaxTier);
        ent.Comp.Tier = tier;

        if (ent.Comp.TierSpeeds.Count > 0)
            ent.Comp.MaxLinearSpeed = ent.Comp.TierSpeeds[Math.Clamp(tier - 1, 0, ent.Comp.TierSpeeds.Count - 1)];

        // Lower tiers turn slower.
        if (ent.Comp.TurnTierMults.Count > 0)
            ent.Comp.MaxAngularSpeed = ent.Comp.MaxAngularSpeedBase
                                       * ent.Comp.TurnTierMults[Math.Clamp(tier - 1, 0, ent.Comp.TurnTierMults.Count - 1)];

        // FTL/BSS from FtlTier upward.
        ent.Comp.FtlCapable = tier >= ent.Comp.FtlTier;
        if (ent.Comp.FtlCapable)
            EnsureComp<FlyingCraftJumpComponent>(ent);
        else
            RemComp<FlyingCraftJumpComponent>(ent);

        // Durability scales ×DurabilityPerTier per tier step.
        var durability = ent.Comp.BaseDurability * MathF.Pow(ent.Comp.DurabilityPerTier, tier - 1);
        SetMaxHealth(ent.Owner, durability);

        Dirty(ent);
    }

    /// <summary>Writes the destruction threshold (max hull health) and the networked HUD max-health.</summary>
    private void SetMaxHealth(EntityUid craft, float maxHealth)
    {
        var hp = (int) MathF.Round(maxHealth);

        if (TryComp<DestructibleComponent>(craft, out var destructible))
        {
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is DamageTrigger trigger)
                {
                    trigger.Damage = hp;
                    break;
                }
            }
        }

        if (TryComp<FlyingCraftHudStateComponent>(craft, out var hud))
        {
            hud.MaxHealth = hp;
            Dirty(craft, hud);
        }
    }

    private void OnBoardInserted(Entity<FlyingCraftBoardSlotComponent> slot, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != BoardSlotId || !TryComp<FlyingCraftComponent>(slot, out var craft))
            return;

        var ent = new Entity<FlyingCraftComponent>(slot.Owner, craft);
        var board = args.Entity;

        // Tier upgrade board.
        if (TryComp<FlyingCraftUpgradeBoardComponent>(board, out var upgrade))
        {
            if (ent.Comp.Civilian)
            {
                _popup.PopupEntity(Loc.GetString("flying-craft-board-civilian"), ent);
                return;
            }

            if (upgrade.Tier != ent.Comp.Tier + 1)
            {
                _popup.PopupEntity(Loc.GetString("flying-craft-board-wrong-tier", ("tier", ent.Comp.Tier + 1)), ent);
                return;
            }

            ApplyTier(ent, upgrade.Tier);
            QueueDel(board);
            _popup.PopupEntity(Loc.GetString("flying-craft-board-upgraded", ("tier", ent.Comp.Tier)), ent);
            return;
        }

        // Weaponisation board (civilian -> combat class).
        if (TryComp<FlyingCraftWeaponBoardComponent>(board, out var weaponBoard))
        {
            if (!ent.Comp.Civilian)
            {
                _popup.PopupEntity(Loc.GetString("flying-craft-board-armed"), ent);
                return;
            }

            if (weaponBoard.ForHeavy != ent.Comp.CivilianHeavy)
            {
                _popup.PopupEntity(Loc.GetString("flying-craft-board-wrong-hull"), ent);
                return;
            }

            Weaponize(ent, weaponBoard);
            QueueDel(board);
            _popup.PopupEntity(Loc.GetString("flying-craft-board-weaponised"), ent);
        }
    }

    /// <summary>Converts a civilian craft into the board's combat class (T1), copying its stats + weapons.</summary>
    private void Weaponize(Entity<FlyingCraftComponent> ent, FlyingCraftWeaponBoardComponent board)
    {
        if (!_proto.TryIndex(board.TargetCraft, out var proto))
            return;

        if (proto.TryGetComponent<FlyingCraftComponent>(out var tfc, _factory))
        {
            ent.Comp.Class = tfc.Class;
            ent.Comp.TierSpeeds = new List<float>(tfc.TierSpeeds);
            ent.Comp.BaseDurability = tfc.BaseDurability;
            ent.Comp.DurabilityPerTier = tfc.DurabilityPerTier;
            ent.Comp.MaxAngularSpeedBase = tfc.MaxAngularSpeedBase;
            ent.Comp.TurnTierMults = new List<float>(tfc.TurnTierMults);
            ent.Comp.AccelTime = tfc.AccelTime;
            ent.Comp.BrakeStrength = tfc.BrakeStrength;
            ent.Comp.StrafeMultiplier = tfc.StrafeMultiplier;
            ent.Comp.FtlTier = tfc.FtlTier;
            ent.Comp.MaxTier = tfc.MaxTier;
        }

        ent.Comp.Civilian = false;

        if (proto.TryGetComponent<FlyingCraftWeaponsComponent>(out var tw, _factory))
        {
            var weapons = EnsureComp<FlyingCraftWeaponsComponent>(ent);
            weapons.ReloadMultiplier = tw.ReloadMultiplier;
            weapons.ActiveWeapon = 0;
            weapons.Weapons = new List<FlyingCraftWeapon>();
            foreach (var w in tw.Weapons)
            {
                weapons.Weapons.Add(new FlyingCraftWeapon
                {
                    Projectile = w.Projectile,
                    FireRate = w.FireRate,
                    MagazineSize = w.MagazineSize,
                    ReloadCooldown = w.ReloadCooldown,
                    ProjectileSpeed = w.ProjectileSpeed,
                    MuzzleOffset = w.MuzzleOffset,
                    FireSound = w.FireSound,
                });
            }

            Dirty(ent.Owner, weapons);
        }

        // T1 combat craft; grant the seated pilot the weapon actions (the pilot is NOT ejected).
        ApplyTier(ent, 1);
        _weapons.GrantPilotActions(ent.Owner);
    }
}
