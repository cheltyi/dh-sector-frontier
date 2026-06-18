// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.Shuttles.Events;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.BUI;

public sealed partial class ShuttleConsoleBoundUserInterface
{
    private void DhOpen()
    {
        if (_window == null)
            return;

        _window.OnAutopilotSetTarget += OnAutopilotSetTarget;
        _window.OnAutopilotToggle += OnAutopilotToggle;
    }

    private void OnAutopilotSetTarget(NetEntity? shuttle, NetCoordinates coords, NetEntity? dock)
    {
        SendMessage(new AutopilotSetTargetMessage
        {
            Coordinates = coords,
            Dock = dock,
        });
    }

    private void OnAutopilotToggle(NetEntity? shuttle, bool enabled)
    {
        SendMessage(new AutopilotToggleMessage
        {
            Enabled = enabled,
        });
    }
}
