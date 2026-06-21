using Content.Shared.Atmos;
using Content.Shared._NF.Atmos.Systems;

namespace Content.Shared._NF.Atmos.Components;

[RegisterComponent, Access(typeof(SharedGasDepositSystem))]
public sealed partial class GasDepositComponent : Component
{
    /// <summary>
    /// Gases left in the deposit.
    /// </summary>
    [DataField]
    public GasMixture Deposit = new();

    /// <summary>
    /// The maximum number of moles for this deposit to be considered "mostly depleted".
    /// </summary>
    // Dark Haven - persistence: [DataField] so the precomputed threshold survives a save. It is computed from the
    // ORIGINAL deposit size only at MapInit (not re-raised on load); recomputing later would use the already-
    // reduced TotalMoles and give a wrong (too-low) threshold.
    [DataField]
    public float LowMoles;
}
