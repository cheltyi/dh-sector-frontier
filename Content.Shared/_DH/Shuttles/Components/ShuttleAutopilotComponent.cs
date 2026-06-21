// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._DH.Shuttles.Components;

/// <summary>
/// Autopilot state for a shuttle. Lives on the shuttle GRID (not the console), because one ship has
/// one autopilot regardless of how many consoles it has. The console UI commands it; the server
/// <c>ShuttleAutopilotSystem</c> drives the ship by emulating pilot input (a virtual pilot mob).
///
/// The networked fields are read directly on the client to draw the planned route / target marker
/// and to colour the Start/Stop button — no extra BUI state is needed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShuttleAutopilotComponent : Component
{
    /// <summary>Whether the autopilot is currently engaged and driving the ship.</summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>Tier of the currently active command (capability gate set when a route is built).</summary>
    [DataField, AutoNetworkedField]
    public int Tier;

    /// <summary>
    /// Highest autopilot tier installed across all consoles on this grid (0 = no board).
    /// Used purely for client-side UI gating (graying out buttons / enabling dock selection).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int AvailableTier;

    /// <summary>The chosen destination (free coordinate). Null when targeting a dock or nothing.</summary>
    [AutoNetworkedField]
    public NetCoordinates? Target;

    /// <summary>The chosen target docking port (tier 3 only). Null for free-coordinate travel.</summary>
    [AutoNetworkedField]
    public NetEntity? TargetDock;

    /// <summary>
    /// The planned route as map/world positions, for drawing the polyline on the radar.
    /// Element 0 is near the ship, the last element is the destination.
    /// </summary>
    [AutoNetworkedField]
    public List<Vector2> Route = new();

    // --- server-only runtime state (not networked) ---

    /// <summary>The spawned virtual pilot mob attached to a console; null when not engaged.</summary>
    // Dark Haven - persistence: serialize (server-only) so a mid-autopilot save round-trips to the SAME pilot mob
    // (it lives on this grid, in save scope). On load EnsurePilot reuses it and re-links via AddPilot, so no
    // orphaned/duplicate pilot mob is left behind.
    [ViewVariables, DataField(serverOnly: true)]
    public EntityUid? PilotEntity;

    /// <summary>Index of the route waypoint we are currently flying toward.</summary>
    [ViewVariables]
    public int WaypointIndex;

    /// <summary>Time accumulated since the last successful re-plan.</summary>
    [ViewVariables]
    public float ReplanTimer;

    /// <summary>Time spent without meaningful progress toward the current waypoint.</summary>
    [ViewVariables]
    public float StuckTimer;

    /// <summary>Distance to the current waypoint at the last stuck-check, to detect lack of progress.</summary>
    [ViewVariables]
    public float LastWaypointDistance = float.MaxValue;

    /// <summary>Control-loop time accumulator (fixed-rate stepping).</summary>
    [ViewVariables]
    public float ControlAccum;

    /// <summary>
    /// Latched flip-and-burn state: once we commit to shedding speed (rotate retrograde + thrust) we stay
    /// committed until slow enough, instead of flip-flopping around the speed setpoint.
    /// </summary>
    [ViewVariables]
    public bool Braking;

    /// <summary>Time spent in the final docking maneuver; triggers a snap fallback if it drags on.</summary>
    [ViewVariables]
    public float DockTimer;

    // --- tunables ---

    /// <summary>Degrees the ship's nose is offset from grid +Y. Most shuttles are 0.</summary>
    [DataField]
    public float ForwardAngleOffset;

    /// <summary>Within this distance of the final destination the ship brakes to a stop.</summary>
    [DataField]
    public float ArriveTolerance = 8f;

    /// <summary>Within this distance of an intermediate waypoint we advance to the next one.</summary>
    [DataField]
    public float WaypointTolerance = 12f;
}
