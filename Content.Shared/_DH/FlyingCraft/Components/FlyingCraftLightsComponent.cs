// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._DH.FlyingCraft.Components;

/// <summary>
/// Craft lighting, all as child light entities (an entity can carry only one PointLight): two always-on coloured
/// navigation lights (green port/left, red starboard/right), and a toggleable white "headlight" = a faint soft glow
/// circle + a directional forward beam (cone child rotated 180° locally so autoRot aims it along the nose). The
/// headlight is switched by a granted action (no battery, like a mech light). Shared data only; logic is server-side.
/// </summary>
[RegisterComponent]
public sealed partial class FlyingCraftLightsComponent : Component
{
    /// <summary>Green port (left) navigation light prototype.</summary>
    [DataField]
    public EntProtoId NavLightPort = "FlyingCraftNavLightPort";

    /// <summary>Red starboard (right) navigation light prototype.</summary>
    [DataField]
    public EntProtoId NavLightStarboard = "FlyingCraftNavLightStarboard";

    /// <summary>Local offset for the port light (left side, vertical centre). Nose = +Y, so left = -X.</summary>
    [DataField]
    public Vector2 PortOffset = new(-1f, 0f);

    /// <summary>Local offset for the starboard light (right side, vertical centre).</summary>
    [DataField]
    public Vector2 StarboardOffset = new(1f, 0f);

    [DataField]
    public EntityUid? PortLight;

    [DataField]
    public EntityUid? StarboardLight;

    /// <summary>Faint soft circle around the hull, toggled together with the directional headlight.</summary>
    [DataField]
    public EntProtoId HeadlightGlow = "FlyingCraftHeadlightGlow";

    [DataField]
    public EntityUid? HeadlightGlowLight;

    /// <summary>Directional headlight beam (cone) child, spawned with a 180° local rotation so it aims forward.</summary>
    [DataField]
    public EntProtoId HeadlightBeam = "FlyingCraftHeadlightBeam";

    [DataField]
    public EntityUid? HeadlightBeamLight;

    /// <summary>The "toggle headlight" action granted to the pilot while seated.</summary>
    [DataField]
    public EntProtoId HeadlightAction = "ActionFlyingCraftHeadlight";

    [DataField]
    public EntityUid? HeadlightActionEntity;
}
