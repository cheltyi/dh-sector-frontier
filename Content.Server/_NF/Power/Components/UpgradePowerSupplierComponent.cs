using Content.Server.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._NF.Power.Components;

[RegisterComponent]
public sealed partial class UpgradePowerSupplierComponent : Component
{
    // Dark Haven: [DataField] so the captured base survives a persistence map save (captured only at MapInit,
    // which is not re-raised on load; otherwise a later RefreshParts recomputes MaxSupply from 0). See
    // ThrusterComponent.BaseThrust for the same pattern.
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BaseSupplyRate;

    /// <summary>
    /// The machine part that affects the power supplu.
    /// </summary>
    [DataField("machinePartPowerSupply", customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public string MachinePartPowerSupply = "Capacitor";

    /// <summary>
    /// The multiplier used for scaling the power supply.
    /// </summary>
    [DataField("powerSupplyMultiplier", required: true), ViewVariables(VVAccess.ReadWrite)]
    public float PowerSupplyMultiplier = 1f;

    /// <summary>
    /// What type of scaling is being used?
    /// </summary>
    [DataField("scaling", required: true), ViewVariables(VVAccess.ReadWrite)]
    public MachineUpgradeScalingType Scaling;

    /// <summary>
    /// The current value that the power supply is being scaled by,
    /// </summary>
    [DataField("actualScalar"), ViewVariables(VVAccess.ReadWrite)]
    public float ActualScalar = 1f;
}
