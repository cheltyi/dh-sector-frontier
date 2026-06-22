// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Networked snapshot of the ACTIVE weapon's ammo + reload state, pushed by the server so the cockpit HUD can show
/// it client-side (the per-weapon runtime ammo on <see cref="FlyingCraftWeaponsComponent"/> is server-only). Fuel
/// is read straight from <see cref="FlyingCraftFuelComponent"/> (already networked); speed/coords come from the
/// craft's physics/transform (also networked), so only ammo/reload needs this dedicated state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FlyingCraftHudStateComponent : Component
{
    /// <summary>Active weapon's current rounds (-1 = no weapon / uninitialised; the HUD hides the ammo widget).</summary>
    [DataField, AutoNetworkedField]
    public int Ammo = -1;

    /// <summary>Active weapon's magazine size.</summary>
    [DataField, AutoNetworkedField]
    public int MagazineSize;

    /// <summary>Server CurTime when the active weapon's reload completes; Zero = not reloading.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ReloadEnd;

    /// <summary>Total reload duration, so the client can draw progress = 1 - (ReloadEnd - now) / ReloadDuration.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ReloadDuration;

    /// <summary>The craft's destruction threshold (max health), relayed so the HUD can draw a health bar. 0 = unknown.</summary>
    [DataField, AutoNetworkedField]
    public float MaxHealth;
}
