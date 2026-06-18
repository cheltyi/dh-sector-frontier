// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._DH.Shuttles.Components;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

// Autopilot additions to the shuttle console NAV screen: the "Build Route" picker and the Start/Stop
// toggle. Reads the grid's networked ShuttleAutopilotComponent to drive the button state, so no extra
// BUI state is required.
public sealed partial class NavScreen
{
    /// <summary>Raised with (shuttle, destination, dock?) when the player picks a route target.</summary>
    public event Action<NetEntity?, NetCoordinates, NetEntity?>? OnAutopilotSetTarget;

    /// <summary>Raised with (shuttle, enabled) when the player toggles the autopilot.</summary>
    public event Action<NetEntity?, bool>? OnAutopilotToggle;

    private bool _pickingRoute;
    private Dictionary<NetEntity, List<DockingPortState>> _autopilotDocks = new();

    private const float AutopilotDockPickRange = 8f;

    private void DhInitialize()
    {
        BuildRouteButton.OnPressed += _ => OnBuildRoutePressed();
        RouteToggleButton.OnToggled += args => OnRouteTogglePressed(args.Pressed);
        NavRadar.OnRadarClick += OnAutopilotRadarClick;
    }

    private void DhUpdateState(NavInterfaceState state)
    {
        _autopilotDocks = state.Docks;
    }

    private void OnBuildRoutePressed()
    {
        _pickingRoute = !_pickingRoute;
        NavRadar.DefaultCursorShape = _pickingRoute ? Control.CursorShape.Crosshair : Control.CursorShape.Arrow;
        BuildRouteButton.ModulateSelfOverride = _pickingRoute ? Color.Yellow : null;
    }

    private void OnRouteTogglePressed(bool pressed)
    {
        _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
        OnAutopilotToggle?.Invoke(shuttle, pressed);
    }

    private void OnAutopilotRadarClick(EntityCoordinates coords)
    {
        if (!_pickingRoute)
            return;

        _pickingRoute = false;
        NavRadar.DefaultCursorShape = Control.CursorShape.Arrow;
        BuildRouteButton.ModulateSelfOverride = null;

        _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);

        NetEntity? dock = null;
        if (GetAvailableTier() >= 3)
        {
            // Forgiving, zoom-scaled pick radius so clicking near a port selects it even when zoomed out;
            // clicking open space still flies to that point.
            var pickRange = MathF.Max(AutopilotDockPickRange, NavRadar.WorldRange * 0.08f);
            dock = FindNearestDock(_xformSystem.ToMapCoordinates(coords), pickRange);
        }

        OnAutopilotSetTarget?.Invoke(shuttle, _entManager.GetNetCoordinates(coords), dock);
    }

    private NetEntity? FindNearestDock(MapCoordinates click, float pickRange)
    {
        NetEntity? best = null;
        var bestDist = pickRange;

        foreach (var (_, ports) in _autopilotDocks)
        {
            foreach (var port in ports)
            {
                // GetAllDocks returns docks for the whole map; skip ones whose grid isn't replicated to us
                // (avoids spamming the "invalid entity" error log from ToMapCoordinates).
                var dockCoords = _entManager.GetCoordinates(port.Coordinates);
                if (!_entManager.EntityExists(dockCoords.EntityId))
                    continue;

                var portMap = _xformSystem.ToMapCoordinates(dockCoords, logError: false);
                if (portMap.MapId != click.MapId)
                    continue;

                var dist = (portMap.Position - click.Position).Length();
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = port.Entity;
                }
            }
        }

        return best;
    }

    private int GetAvailableTier()
    {
        return _entManager.TryGetComponent<ShuttleAutopilotComponent>(_shuttleEntity, out var ap)
            ? ap.AvailableTier
            : 0;
    }

    private void DhFrameUpdate()
    {
        var hasComp = _entManager.TryGetComponent<ShuttleAutopilotComponent>(_shuttleEntity, out var ap);
        var tier = hasComp ? ap!.AvailableTier : 0;
        var enabled = hasComp && ap!.Enabled;

        // If the board was pulled while picking, cancel pick mode so the cursor/highlight don't get stuck.
        if (tier <= 0 && _pickingRoute)
        {
            _pickingRoute = false;
            NavRadar.DefaultCursorShape = Control.CursorShape.Arrow;
            BuildRouteButton.ModulateSelfOverride = null;
        }

        BuildRouteButton.Disabled = tier <= 0;
        RouteToggleButton.Disabled = tier <= 0;
        RouteToggleButton.Pressed = enabled;
        RouteToggleButton.Text = Loc.GetString(enabled ? "shuttle-console-route-stop" : "shuttle-console-route-start");
        RouteToggleButton.ModulateSelfOverride = tier <= 0 ? null : (enabled ? Color.Red : Color.LimeGreen);

        // Highlight the chosen docking port on the radar (drawn at the exact spot the radar draws the dock).
        NavRadar.HighlightDockPort = hasComp ? ap!.TargetDock : null;
    }
}
