// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.Needs.EntitySystems;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._DH.Needs.Components;

/// <summary>
/// Need to sleep. <see cref="Value"/> rises while awake (~3 hours to <see cref="Max"/>) and drains while the
/// mob is asleep. As it rises the mob slows down; at <see cref="Max"/> the mob passes out on its own. Only on
/// organic species (robots/silicon don't have this component, so they have no sleep need).
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SleepNeedSystem))]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SleepNeedComponent : Component
{
    /// <summary>Current tiredness, 0 (rested) .. <see cref="Max"/> (passing out).</summary>
    [DataField, AutoNetworkedField]
    public float Value;

    [DataField, AutoNetworkedField]
    public float Max = 200f;

    /// <summary>Tiredness gained per second while awake. 0.0185 ≈ 3 hours from 0 to Max (200).</summary>
    [DataField, AutoNetworkedField]
    public float RiseRate = 0.0185f;

    /// <summary>Tiredness lost per second while asleep. ~0.6 clears a full meter in ~5.5 minutes of sleep.</summary>
    [DataField, AutoNetworkedField]
    public float SleepDrainRate = 0.6f;

    // Bands: below Tired = no effect; Tired = mild slow; Exhausted = strong slow; Max = pass out.
    [DataField, AutoNetworkedField]
    public float TiredThreshold = 120f;

    [DataField, AutoNetworkedField]
    public float ExhaustedThreshold = 165f;

    [DataField, AutoNetworkedField]
    public float TiredSlowdown = 0.9f;

    [DataField, AutoNetworkedField]
    public float ExhaustedSlowdown = 0.7f;

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> TiredAlert = "SleepTired";

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> ExhaustedAlert = "SleepExhausted";

    [DataField, AutoNetworkedField]
    public ProtoId<AlertCategoryPrototype> AlertCategory = "Sleep";

    /// <summary>True if the sleep need forced this mob to pass out, so it auto-wakes once rested (Value 0).</summary>
    [DataField, AutoNetworkedField]
    public bool PassedOut;

    /// <summary>Last computed band (0 none, 1 tired, 2 exhausted) — drives alert/movement refresh on change.</summary>
    [ViewVariables]
    public int LastBand;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField, AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}
