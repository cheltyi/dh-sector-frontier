// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._DH.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    /// <summary>When set, a click on the map picks an autopilot destination instead of an FTL jump.</summary>
    public bool AutopilotPickMode;

    /// <summary>Raised with the clicked map coordinate while <see cref="AutopilotPickMode"/> is on.</summary>
    public event Action<MapCoordinates>? RequestAutopilotTarget;

    /// <summary>
    /// Draws the planned autopilot route (polyline) and the destination marker on the big sector map,
    /// mirroring the NAV radar overlay. Route points are world positions on the shuttle's current map, so
    /// they're transformed the same way the map draws everything else (inverse offset, flip Y, scale).
    /// </summary>
    private void DhDrawAutopilot(DrawingHandleScreen handle, Matrix3x2 worldToMap)
    {
        if (_shuttleEntity is not { } grid)
            return;

        if (!EntManager.TryGetComponent<TransformComponent>(grid, out var gridXform) || gridXform.MapID != ViewingMap)
            return;

        if (!EntManager.TryGetComponent<ShuttleAutopilotComponent>(grid, out var ap) || ap.Route.Count == 0)
            return;

        Vector2 ToScreen(Vector2 world)
        {
            var adjusted = Vector2.Transform(world, worldToMap);
            return ScalePosition(adjusted with { Y = -adjusted.Y });
        }

        // Docking target -> blue; free coordinate -> cyan.
        var color = ap.TargetDock != null ? Color.FromHex("#33A0FF") : Color.Cyan;
        var srgb = Color.ToSrgb(color);
        var lineColor = srgb.WithAlpha(0.5f);

        // Polyline from the ship CENTRE (the point the server anchors Route[0] to) through the waypoints.
        var startWorld = EntManager.TryGetComponent<MapGridComponent>(grid, out var gridComp)
            ? Vector2.Transform(gridComp.LocalAABB.Center, _xformSystem.GetWorldMatrix(grid))
            : _xformSystem.GetWorldPosition(grid);

        var prev = ToScreen(startWorld);
        foreach (var waypoint in ap.Route)
        {
            var point = ToScreen(waypoint);
            handle.DrawLine(prev, point, lineColor);
            prev = point;
        }

        var marker = ToScreen(ap.Route[^1]);
        handle.DrawCircle(marker, 6f, srgb, false);
        handle.DrawCircle(marker, 2.5f, srgb, true);
    }
}
