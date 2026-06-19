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
/// Combined bladder/bowel need. <see cref="Value"/> rises over time (by default twice as slow as hunger) and
/// faster the more the mob eats and drinks; at high values the mob slows down (like hunger/thirst). It drains
/// to empty while the mob sits on a toilet. Only on organic species (robots have no bladder).
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(BladderSystem))]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BladderComponent : Component
{
    /// <summary>Current fullness, 0 (empty) .. <see cref="Max"/> (bursting).</summary>
    [DataField, AutoNetworkedField]
    public float Value;

    [DataField, AutoNetworkedField]
    public float Max = 200f;

    /// <summary>Passive fill per second. 0.01 is half of hunger's 0.02 base ("twice as slow as hunger").</summary>
    [DataField, AutoNetworkedField]
    public float RiseRate = 0.01f;

    /// <summary>Fullness lost per second while sitting on a toilet (empties a full bladder in a few seconds).</summary>
    [DataField, AutoNetworkedField]
    public float RelieveRate = 40f;

    /// <summary>Added to the bladder each time the mob eats / drinks (drinking adds more).</summary>
    [DataField, AutoNetworkedField]
    public float FoodFill = 8f;

    [DataField, AutoNetworkedField]
    public float DrinkFill = 16f;

    // Bands: below Full = no effect; Full = alert; Bursting = alert + slow.
    [DataField, AutoNetworkedField]
    public float FullThreshold = 130f;

    [DataField, AutoNetworkedField]
    public float BurstingThreshold = 175f;

    [DataField, AutoNetworkedField]
    public float BurstingSlowdown = 0.75f;

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> FullAlert = "BladderFull";

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> BurstingAlert = "BladderBursting";

    [DataField, AutoNetworkedField]
    public ProtoId<AlertCategoryPrototype> AlertCategory = "Bladder";

    /// <summary>Last computed band (0 none, 1 full, 2 bursting) — drives alert/movement refresh on change.</summary>
    [ViewVariables]
    public int LastBand;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdateTime;

    [DataField, AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(1);
}
