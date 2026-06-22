// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Fires a flying craft's fixed nose-mounted weapons (you aim by turning the craft — in combat mode the craft
/// auto-turns toward the cursor). Each weapon has a magazine and a flat cooldown reload (no charging/ammo items);
/// higher tiers reload faster via ReloadMultiplier. Only the ACTIVE weapon fires; the pilot holds LMB to fire it
/// (relayed via <see cref="FlyingCraftSetFiringEvent"/>) and switches weapons with a granted action. Projectiles
/// inherit the craft's velocity.
/// </summary>
public sealed class FlyingCraftWeaponSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftWeaponsComponent, EntInsertedIntoContainerMessage>(OnPilotInserted);
        SubscribeLocalEvent<FlyingCraftWeaponsComponent, EntRemovedFromContainerMessage>(OnPilotRemoved);
        SubscribeLocalEvent<FlyingCraftWeaponsComponent, FlyingCraftSwapWeaponActionEvent>(OnSwapWeapon);
        SubscribeNetworkEvent<FlyingCraftSetFiringEvent>(OnSetFiring);
    }

    private void OnPilotInserted(Entity<FlyingCraftWeaponsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != FlyingCraftPilotSystem.PilotContainerId)
            return;

        // Start on the primary weapon; only offer the swap action when there is more than one weapon.
        ent.Comp.ActiveWeapon = 0;
        Dirty(ent);

        if (ent.Comp.Weapons.Count > 1)
            _actions.AddAction(args.Entity, ref ent.Comp.SwapActionEntity, ent.Comp.SwapWeaponAction, ent.Owner);
    }

    /// <summary>Grants the seated pilot the weapon actions — used after a civilian is weaponised mid-flight.</summary>
    public void GrantPilotActions(EntityUid craft)
    {
        if (!TryComp<FlyingCraftWeaponsComponent>(craft, out var comp)
            || !_container.TryGetContainer(craft, FlyingCraftPilotSystem.PilotContainerId, out var container)
            || container is not ContainerSlot { ContainedEntity: { } pilot })
            return;

        comp.ActiveWeapon = 0;
        Dirty(craft, comp);

        if (comp.Weapons.Count > 1 && comp.SwapActionEntity == null)
            _actions.AddAction(pilot, ref comp.SwapActionEntity, comp.SwapWeaponAction, craft);
    }

    private void OnPilotRemoved(Entity<FlyingCraftWeaponsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != FlyingCraftPilotSystem.PilotContainerId)
            return;

        _actions.RemoveAction(args.Entity, ent.Comp.SwapActionEntity);
        ent.Comp.SwapActionEntity = null;
        foreach (var weapon in ent.Comp.Weapons)
            weapon.Firing = false;
    }

    private void OnSwapWeapon(Entity<FlyingCraftWeaponsComponent> ent, ref FlyingCraftSwapWeaponActionEvent args)
    {
        args.Handled = true;
        if (ent.Comp.Weapons.Count <= 1)
            return;

        // Switching weapons stops fire on the old one.
        foreach (var weapon in ent.Comp.Weapons)
            weapon.Firing = false;

        ent.Comp.ActiveWeapon = (ent.Comp.ActiveWeapon + 1) % ent.Comp.Weapons.Count;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("flying-craft-weapon-selected", ("index", ent.Comp.ActiveWeapon + 1)),
            args.Performer, args.Performer);
    }

    /// <summary>Client relay: the seated pilot pressed/released LMB. Toggles the active weapon's sustained fire.</summary>
    private void OnSetFiring(FlyingCraftSetFiringEvent msg, EntitySessionEventArgs args)
    {
        // Combat mode is the pilot's OWN vanilla combat mode.
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<FlyingCraftPilotComponent>(pilot, out var pilotComp)
            || !TryComp<CombatModeComponent>(pilot, out var cm) || !cm.IsInCombatMode)
            return;

        var craft = GetEntity(msg.Craft);
        if (pilotComp.Craft != craft || !TryComp<FlyingCraftWeaponsComponent>(craft, out var weapons))
            return;

        if (weapons.ActiveWeapon < 0 || weapons.ActiveWeapon >= weapons.Weapons.Count)
            return;

        weapons.Weapons[weapons.ActiveWeapon].Firing = msg.Firing;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FlyingCraftWeaponsComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var body, out var xform))
        {
            var map = _xform.GetMapCoordinates(uid, xform);
            if (map.MapId == MapId.Nullspace)
                continue;

            var nose = _xform.GetWorldRotation(uid).RotateVec(Vector2.UnitY);
            var craftVel = body.LinearVelocity;

            // Higher tier reloads faster (T1 = x1.0 .. T5 = x0.52), combined with the per-craft multiplier.
            var tier = TryComp<FlyingCraftComponent>(uid, out var craft) ? craft.Tier : 1;
            var reloadScale = comp.ReloadMultiplier * MathF.Max(0.4f, 1f - (tier - 1) * 0.12f);

            for (var i = 0; i < comp.Weapons.Count; i++)
            {
                var weapon = comp.Weapons[i];

                if (weapon.Count < 0)
                    weapon.Count = weapon.MagazineSize;

                // Cooldown reload complete -> refill. (Runs for every weapon so a swapped-away one keeps reloading.)
                if (weapon.ReloadEnd != TimeSpan.Zero && now >= weapon.ReloadEnd)
                {
                    weapon.Count = weapon.MagazineSize;
                    weapon.ReloadEnd = TimeSpan.Zero;
                }

                // Only the active weapon ever fires.
                if (i != comp.ActiveWeapon
                    || !weapon.Firing || weapon.ReloadEnd != TimeSpan.Zero || weapon.Count <= 0 || now < weapon.NextFire)
                    continue;

                // Fire one shot forward along the nose, exactly like a held gun: ShootProjectile sets
                // BodyStatus.InAir (so tile friction never slows it -> flies forever), the correct world-vec
                // rotation (sprite faces travel) + per-proto sprite offset, and a map-velocity-aware launch.
                var spawnPos = new MapCoordinates(map.Position + nose * weapon.MuzzleOffset, map.MapId);
                var proj = Spawn(weapon.Projectile, spawnPos);
                _gun.ShootProjectile(proj, nose, craftVel, uid, uid, weapon.ProjectileSpeed);
                if (weapon.FireSound != null)
                    _audio.PlayPvs(weapon.FireSound, uid);

                weapon.Count--;
                weapon.NextFire = now + TimeSpan.FromSeconds(1f / MathF.Max(weapon.FireRate, 0.1f));

                // Out of ammo -> start the flat cooldown reload (shorter at higher tiers).
                if (weapon.Count <= 0)
                    weapon.ReloadEnd = now + weapon.ReloadCooldown * reloadScale;
            }

            // Publish the active weapon's ammo/reload to the networked HUD state (diff-guarded to avoid Dirty spam:
            // Count only changes on a shot frame, ReloadEnd on empty/refill).
            if (TryComp<FlyingCraftHudStateComponent>(uid, out var hud)
                && comp.ActiveWeapon >= 0 && comp.ActiveWeapon < comp.Weapons.Count)
            {
                var active = comp.Weapons[comp.ActiveWeapon];
                var dur = active.ReloadCooldown * reloadScale;
                if (hud.Ammo != active.Count || hud.MagazineSize != active.MagazineSize
                    || hud.ReloadEnd != active.ReloadEnd || hud.ReloadDuration != dur)
                {
                    hud.Ammo = active.Count;
                    hud.MagazineSize = active.MagazineSize;
                    hud.ReloadEnd = active.ReloadEnd;
                    hud.ReloadDuration = dur;
                    Dirty(uid, hud);
                }
            }
        }
    }
}
