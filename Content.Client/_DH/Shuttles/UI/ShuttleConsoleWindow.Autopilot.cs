// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    public event Action<NetEntity?, NetCoordinates, NetEntity?>? OnAutopilotSetTarget;
    public event Action<NetEntity?, bool>? OnAutopilotToggle;

    private void DhInitialize()
    {
        NavContainer.OnAutopilotSetTarget += (shuttle, coords, dock) => OnAutopilotSetTarget?.Invoke(shuttle, coords, dock);
        NavContainer.OnAutopilotToggle += (shuttle, enabled) => OnAutopilotToggle?.Invoke(shuttle, enabled);
    }
}
