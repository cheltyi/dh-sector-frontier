// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.Shuttles.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

// Lets the player pick an autopilot destination anywhere on the big sector map (not just the small NAV
// radar). The button arms a pick mode on the map control; a click there resolves to a map coordinate
// which we forward up as a route target. Capability gating mirrors the NAV screen (reads the grid's
// networked ShuttleAutopilotComponent).
public sealed partial class MapScreen
{
    /// <summary>Raised with the map coordinate the player picked for an autopilot route (no dock on the big map).</summary>
    public event Action<NetCoordinates>? OnAutopilotMapTarget;

    private void DhInitialize()
    {
        MapAutopilotButton.OnToggled += DhAutopilotToggled;
        MapRadar.RequestAutopilotTarget += DhOnAutopilotTargetPicked;
    }

    private void DhAutopilotToggled(BaseButton.ButtonToggledEventArgs args)
    {
        MapRadar.AutopilotPickMode = args.Pressed;
        MapRadar.DefaultCursorShape = args.Pressed ? Control.CursorShape.Crosshair : Control.CursorShape.Arrow;

        // FTL targeting and autopilot picking are mutually exclusive — arming one disarms the other.
        if (args.Pressed)
        {
            MapFTLButton.Pressed = false; // triggers FtlPreviewToggled -> FtlMode = false
            MapRadar.ShowFTLRangeOnly = false;
        }
    }

    /// <summary>Called from the base FTL toggle so arming FTL cancels an in-progress autopilot pick.</summary>
    private void DhCancelAutopilotPick()
    {
        if (!MapAutopilotButton.Pressed && !MapRadar.AutopilotPickMode)
            return;

        MapAutopilotButton.Pressed = false;
        MapRadar.AutopilotPickMode = false;
        MapRadar.DefaultCursorShape = Control.CursorShape.Arrow;
    }

    private void DhOnAutopilotTargetPicked(MapCoordinates coords)
    {
        // Disarm the pick mode (single-shot) and forward the world point as a route target.
        MapAutopilotButton.Pressed = false;
        MapRadar.AutopilotPickMode = false;
        MapRadar.DefaultCursorShape = Control.CursorShape.Arrow;

        if (coords.MapId == MapId.Nullspace)
            return;

        var mapUid = _mapManager.GetMapEntityId(coords.MapId);
        if (!_entManager.EntityExists(mapUid))
            return;

        var netCoords = _entManager.GetNetCoordinates(new EntityCoordinates(mapUid, coords.Position));
        OnAutopilotMapTarget?.Invoke(netCoords);
    }

    private void DhFrameUpdate()
    {
        var tier = _entManager.TryGetComponent<ShuttleAutopilotComponent>(_shuttleEntity, out var ap)
            ? ap.AvailableTier
            : 0;

        // If the board was pulled while picking, cancel pick mode so the cursor/button don't get stuck armed.
        if (tier <= 0 && (MapAutopilotButton.Pressed || MapRadar.AutopilotPickMode))
            DhCancelAutopilotPick();

        MapAutopilotButton.Disabled = tier <= 0;
    }
}
