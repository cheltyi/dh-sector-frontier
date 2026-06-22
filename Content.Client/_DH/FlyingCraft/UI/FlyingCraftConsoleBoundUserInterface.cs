// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.FlyingCraft;
using Robust.Client.UserInterface;

namespace Content.Client._DH.FlyingCraft.UI;

public sealed class FlyingCraftConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FlyingCraftConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FlyingCraftConsoleWindow>();
        _window.BuyPressed += OnBuy;
    }

    private void OnBuy(string proto, NetEntity? spawner)
    {
        SendMessage(new FlyingCraftBuyMessage(proto, spawner));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FlyingCraftConsoleState consoleState)
            _window?.Update(consoleState);
    }
}
