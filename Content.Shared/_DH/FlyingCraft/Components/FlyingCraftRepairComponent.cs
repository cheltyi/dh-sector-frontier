// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Lets a flying craft be repaired by opening its technical panel with a SCREWDRIVER and then inserting component
/// stacks (steel + cables). When the full tier bill is inserted the hull heals <see cref="HealAmount"/>. Examining
/// the craft with the panel open shows what's still needed. Flight is disabled while the panel is open.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlyingCraftRepairComponent : Component
{
    /// <summary>Hull restored when a full bill of components is inserted.</summary>
    [DataField]
    public float HealAmount = 100f;

    /// <summary>Whether the technical panel is open (required to insert components; blocks flight).</summary>
    [DataField, AutoNetworkedField]
    public bool PanelOpen;

    /// <summary>Runtime: components inserted so far toward the current repair (stack type id -> count).</summary>
    [ViewVariables]
    public Dictionary<string, int> Inserted = new();
}
