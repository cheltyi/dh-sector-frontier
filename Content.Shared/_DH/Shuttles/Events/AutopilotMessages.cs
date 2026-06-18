// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._DH.Shuttles.Events;

/// <summary>
/// Client → server: set the autopilot destination from a point clicked on the radar.
/// If <see cref="Dock"/> is set (tier 3), the destination is that docking port instead of the raw point.
/// The server builds the route immediately but does not start flying until an enable toggle arrives.
/// </summary>
[Serializable, NetSerializable]
public sealed class AutopilotSetTargetMessage : BoundUserInterfaceMessage
{
    public NetCoordinates Coordinates { get; set; }
    public NetEntity? Dock { get; set; }
}

/// <summary>
/// Client → server: engage (true) or disengage (false) the autopilot toward the current target.
/// </summary>
[Serializable, NetSerializable]
public sealed class AutopilotToggleMessage : BoundUserInterfaceMessage
{
    public bool Enabled { get; set; }
}

/// <summary>
/// Server-internal: raised on a shuttle console when something is inserted into / removed from its
/// "autopilot_slot". The base disk-slot handler forwards this so the autopilot system (which cannot
/// add a second subscription to the same container event) can recompute the grid's available tier.
/// </summary>
[ByRefEvent]
public readonly record struct AutopilotBoardChangedEvent;
