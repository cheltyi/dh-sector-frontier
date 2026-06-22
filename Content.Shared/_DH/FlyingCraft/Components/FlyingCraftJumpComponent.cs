// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Gives an FTL-capable craft (tier 3+) a short-range bluespace "jump"/BSS (characteristic #7): the pilot opens the
/// HUD radar MAP mode, clicks a destination, and after a per-tier charge (5/4/3 s for tier 3/4/5) the whole craft
/// teleports there (clamped to range), consuming bluespace fuel and going on cooldown. Bespoke single-entity jump —
/// the stock shuttle FTL pipeline is grid-only and cannot move a mech-style entity. Presence of this component IS
/// the FTL-capability gate (authored only on tier 3-5 protos).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FlyingCraftJumpComponent : Component
{
    /// <summary>Max jump distance in tiles (a distant MAP-radar click is clamped to this).</summary>
    [DataField]
    public float MaxRange = 256f;

    /// <summary>Cooldown between jumps.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    /// <summary>Earliest time the craft may jump again.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField, AutoNetworkedField]
    public TimeSpan NextJump;
}
