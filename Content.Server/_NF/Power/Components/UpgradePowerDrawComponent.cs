using Content.Server.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._NF.Power.Components;

/// <summary>
/// This is used for machines whose power draw
/// can be decreased through machine part upgrades.
/// </summary>
[RegisterComponent]
public sealed partial class UpgradePowerDrawComponent : Component
{
    /// <summary>
    /// The base power draw of the machine.
    /// Prioritizes hv/mv draw over lv draw.
    /// Value is initializezd on map init from <see cref="ApcPowerReceiverComponent"/>
    /// </summary>
    // Dark Haven: [DataField] so the captured base survives a persistence map save. MapInit (where it is
    // captured) is NOT re-raised for loaded post-init entities, so without serializing it a later
    // RefreshParts would recompute power from 0. Same reason ThrusterComponent.BaseThrust is serialized.
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BaseLoad;

    /// <summary>
    /// The machine part that affects the power draw.
    /// </summary>
    [DataField("machinePartPowerDraw", customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string MachinePartPowerDraw = "Capacitor";

    /// <summary>
    /// The multiplier used for scaling the power draw.
    /// </summary>
    [DataField("powerDrawMultiplier", required: true), ViewVariables(VVAccess.ReadWrite)]
    public float PowerDrawMultiplier = 1f;

    /// <summary>
    /// What type of scaling is being used?
    /// </summary>
    [DataField("scaling", required: true), ViewVariables(VVAccess.ReadWrite)]
    public MachineUpgradeScalingType Scaling;
}
