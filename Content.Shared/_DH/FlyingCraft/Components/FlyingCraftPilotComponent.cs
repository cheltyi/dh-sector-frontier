// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.GameStates;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Put on the seated pilot for as long as they occupy a flying craft. A mech-style redirect (mirrors
/// MechPilotComponent) so combat-mode systems on the client/server can hop pilot -> craft: the pilot is the
/// player's attached/networked entity, but the craft is what actually flies and fires.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlyingCraftPilotComponent : Component
{
    /// <summary>The craft this pilot is seated in.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid Craft;
}
