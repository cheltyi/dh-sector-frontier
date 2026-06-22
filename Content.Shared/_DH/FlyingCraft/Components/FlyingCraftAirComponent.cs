// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared.Atmos;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// The craft's sealed internal cockpit atmosphere. The seated pilot's lungs draw from (and exhale into) this closed
/// <see cref="GasMixture"/> instead of the space tile under the craft (mirrors MechAirComponent). Pre-filled with
/// breathable gas in the prototype so the pilot doesn't suffocate the moment they launch into vacuum. Shared (data
/// only) so the prototype loads cleanly on both sides; the breathing redirect lives in the server pilot system.
/// </summary>
[RegisterComponent]
public sealed partial class FlyingCraftAirComponent : Component
{
    /// <summary>The cockpit gas. <c>[DataField]</c> so it is authorable as <c>air:</c> in YAML.</summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public GasMixture Air = new(GasMixVolume);

    /// <summary>Sealed cockpit volume (no scrubber off-grid, so make it big enough for a sortie).</summary>
    public const float GasMixVolume = 200f;
}
