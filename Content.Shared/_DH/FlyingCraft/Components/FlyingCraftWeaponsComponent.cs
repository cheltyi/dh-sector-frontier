// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Forward-firing weapons for a flying craft. Each class carries 1-2 fixed nose-mounted weapons (you aim by
/// turning the craft). A weapon has a magazine; when emptied it auto-reloads after a flat COOLDOWN (no charging,
/// no ammo items) — higher tiers shorten the cooldown via <see cref="ReloadMultiplier"/>. Pilots toggle each
/// weapon's sustained fire with a granted action.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlyingCraftWeaponsComponent : Component
{
    /// <summary>The craft's weapons (index 0 = primary, 1 = secondary). Only the active one fires.</summary>
    [DataField]
    public List<FlyingCraftWeapon> Weapons = new();

    /// <summary>Index into <see cref="Weapons"/> of the weapon that the pilot's LMB currently fires.</summary>
    [DataField, AutoNetworkedField]
    public int ActiveWeapon;

    /// <summary>Tier scalar applied to every weapon's reload cooldown (smaller = faster reload at higher tier).</summary>
    [DataField]
    public float ReloadMultiplier = 1f;

    /// <summary>The "switch weapon" action granted to the pilot when the craft carries more than one weapon.</summary>
    [DataField]
    public EntProtoId SwapWeaponAction = "ActionFlyingCraftSwapWeapon";

    /// <summary>Tracked granted swap action entity (removed when the pilot leaves).</summary>
    [DataField]
    public EntityUid? SwapActionEntity;
}

/// <summary>One fixed nose-mounted weapon: config (authored) + runtime ammo/cooldown state.</summary>
[DataDefinition]
public sealed partial class FlyingCraftWeapon
{
    /// <summary>Projectile entity fired.</summary>
    [DataField(required: true)]
    public EntProtoId Projectile;

    /// <summary>Shots per second while firing.</summary>
    [DataField]
    public float FireRate = 4f;

    /// <summary>Shots before the cooldown reload.</summary>
    [DataField]
    public int MagazineSize = 20;

    /// <summary>Base reload cooldown once the magazine empties (scaled by the component's ReloadMultiplier).</summary>
    [DataField]
    public TimeSpan ReloadCooldown = TimeSpan.FromSeconds(4);

    /// <summary>Muzzle velocity of the projectile (added to the craft's own velocity).</summary>
    [DataField]
    public float ProjectileSpeed = 28f;

    /// <summary>Forward muzzle offset from the craft centre (so shots clear the hull).</summary>
    [DataField]
    public float MuzzleOffset = 2f;

    [DataField]
    public SoundSpecifier? FireSound;

    // --- runtime (server) ---
    [ViewVariables] public bool Firing;
    [ViewVariables] public int Count = -1; // -1 = uninitialised -> set to MagazineSize on first use
    [ViewVariables] public TimeSpan NextFire;
    [ViewVariables] public TimeSpan ReloadEnd; // TimeSpan.Zero = not reloading
}
