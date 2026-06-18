// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._DH.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    /// <summary>
    /// Draws the planned autopilot route (polyline) and the destination marker, read directly from the
    /// grid's networked <see cref="ShuttleAutopilotComponent"/>. Route points are map/world positions.
    /// </summary>
    private void DhDrawAutopilot(DrawingHandleScreen handle, Matrix3x2 worldToShuttle, Matrix3x2 shuttleToView)
    {
        if (_coordinates == null)
            return;

        var center = _coordinates.Value.EntityId;
        if (!EntManager.TryGetComponent<TransformComponent>(center, out var centerXform))
            return;

        var grid = centerXform.GridUid ?? center;
        if (!EntManager.TryGetComponent<ShuttleAutopilotComponent>(grid, out var ap) || ap.Route.Count == 0)
            return;

        var worldToView = worldToShuttle * shuttleToView;

        // Docking target -> blue line + marker on the dock; free coordinate -> cyan line + marker at the point.
        var color = ap.TargetDock != null ? Color.FromHex("#33A0FF") : Color.Cyan;
        var srgb = Color.ToSrgb(color);
        var lineColor = srgb.WithAlpha(0.5f);

        // Polyline: ship CENTRE (the same point the server anchors Route[0] to) -> waypoints -> destination.
        var startWorld = EntManager.TryGetComponent<MapGridComponent>(grid, out var gridComp)
            ? Vector2.Transform(gridComp.LocalAABB.Center, _transform.GetWorldMatrix(grid))
            : _transform.GetWorldPosition(grid);
        var prev = Vector2.Transform(startWorld, worldToView);
        foreach (var waypoint in ap.Route)
        {
            var point = Vector2.Transform(waypoint, worldToView);
            handle.DrawLine(prev, point, lineColor);
            prev = point;
        }

        // Marker on the destination. For a dock, take the gate position from the SAME nav-state dock data the
        // radar draws from (so it lands exactly on the highlighted gate even when the station entity is out of
        // PVS); the chosen gate is also gold-highlighted via NavRadar.HighlightDockPort.
        var markerWorld = ap.Route[^1];
        if (ap.TargetDock is { } dockNet && TryGetDockWorld(dockNet, out var dockWorld))
        {
            markerWorld = dockWorld;
            handle.DrawLine(prev, Vector2.Transform(markerWorld, worldToView), lineColor);
        }

        var marker = Vector2.Transform(markerWorld, worldToView);
        handle.DrawCircle(marker, 7f, srgb, false);
        handle.DrawCircle(marker, 2.5f, srgb, true);
    }

    /// <summary>
    /// World position of a docking port, taken from the radar's own nav-state dock data (grid-local position +
    /// the grid's world matrix) — the same source <c>DrawDocks</c> uses, so the marker coincides with the drawn
    /// (and highlighted) gate.
    /// </summary>
    private bool TryGetDockWorld(NetEntity dockNet, out Vector2 world)
    {
        world = default;
        foreach (var (gridNet, ports) in _docks)
        {
            foreach (var port in ports)
            {
                if (port.Entity != dockNet)
                    continue;
                if (!EntManager.TryGetComponent<TransformComponent>(EntManager.GetEntity(gridNet), out var gridXform))
                    return false;
                world = Vector2.Transform(port.Coordinates.Position, _transform.GetWorldMatrix(gridXform));
                return true;
            }
        }

        return false;
    }
}
