// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.CombatMode;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._DH.FlyingCraft;

/// <summary>
/// Client side of flying-craft combat mode. While the local player is the seated pilot of a craft in combat mode,
/// it polls held-LMB (full-auto fire of the active weapon) and the cursor direction (so the craft turns its nose
/// toward the mouse), relaying both to the server. Mirrors the structure of the vanilla
/// <c>GunSystem.Update</c>/<c>MouseRotatorSystem.Update</c> input polling, but targets the craft (via
/// <see cref="FlyingCraftPilotComponent"/>) instead of the player's own entity.
/// </summary>
public sealed class FlyingCraftCombatSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InputSystem _inputSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>Only relay a new aim angle once the cursor has moved this far (rad) — avoids per-tick spam.</summary>
    private const double AimTolerance = 0.02;

    private bool _firing;
    private bool _braking;
    private int _turn;
    private Angle? _lastAim;
    private NetEntity? _lastCraft;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
    }

    /// <summary>HUD relay: a cockpit control button was pressed (the UIController has no RaiseNetworkEvent).</summary>
    public void SendHudButton(NetEntity craft, FlyingCraftHudButton button)
        => RaiseNetworkEvent(new FlyingCraftHudButtonEvent { Craft = craft, Button = button });

    /// <summary>HUD relay: the pilot picked an FTL/BSS destination on the radar MAP.</summary>
    public void SendBeginFtl(NetEntity craft, NetCoordinates destination)
        => RaiseNetworkEvent(new FlyingCraftBeginFtlEvent { Craft = craft, Destination = destination });

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        // Must be piloting a craft. Combat mode is NOT required here — the brake works in any mode.
        if (_player.LocalEntity is not { } player
            || !TryComp<FlyingCraftPilotComponent>(player, out var pilot)
            || !HasComp<FlyingCraftComponent>(pilot.Craft))
        {
            ReleaseAll();
            return;
        }

        var craftUid = pilot.Craft;
        var netCraft = GetNetEntity(craftUid);
        _lastCraft = netCraft;

        // Brake (Space) — held flip-state, works whether or not combat mode is on.
        var braking = _inputSystem.CmdStates.GetState(ContentKeyFunctions.FlyingCraftBrake) == BoundKeyState.Down;
        if (braking != _braking)
        {
            _braking = braking;
            RaiseNetworkEvent(new FlyingCraftSetBrakingEvent { Craft = netCraft, Braking = braking });
        }

        // Manual rotate (Q/E) — held flip-state, works in any mode. Q = left/CCW (+1), E = right/CW (-1).
        var turn = 0;
        if (_inputSystem.CmdStates.GetState(ContentKeyFunctions.FlyingCraftRotateLeft) == BoundKeyState.Down) turn += 1;
        if (_inputSystem.CmdStates.GetState(ContentKeyFunctions.FlyingCraftRotateRight) == BoundKeyState.Down) turn -= 1;
        if (turn != _turn)
        {
            _turn = turn;
            RaiseNetworkEvent(new FlyingCraftSetTurnEvent { Craft = netCraft, Turn = turn });
        }

        // Fire + cursor-aim only while the pilot's own (vanilla) combat mode is on.
        if (!TryComp<CombatModeComponent>(player, out var cm) || !cm.IsInCombatMode)
        {
            ReleaseFire();
            return;
        }

        // Held-LMB full-auto: only send when the held state flips (press -> true, release -> false).
        var firing = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use) == BoundKeyState.Down;
        if (firing != _firing)
        {
            _firing = firing;
            RaiseNetworkEvent(new FlyingCraftSetFiringEvent { Craft = netCraft, Firing = firing });
        }

        // Cursor aim -> desired body rotation. Nose is sprite +Y (north), so the body angle is the cursor's math
        // angle minus 90 degrees (matches the thrust/fire direction RotateVec(Vector2.UnitY)).
        if (!_input.MouseScreenPosition.IsValid)
            return;

        var mapPos = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mapPos.MapId == MapId.Nullspace)
            return;

        var delta = mapPos.Position - _xform.GetMapCoordinates(craftUid).Position;
        if (delta.LengthSquared() < 0.0001f)
            return;

        var goal = delta.ToAngle() - Angle.FromDegrees(90);
        if (_lastAim == null || Math.Abs(Angle.ShortestDistance(_lastAim.Value, goal).Theta) > AimTolerance)
        {
            _lastAim = goal;
            RaiseNetworkEvent(new FlyingCraftSetAimEvent { Craft = netCraft, Rotation = goal });
        }
    }

    /// <summary>Stop firing (e.g. on leaving combat mode) so the active weapon doesn't stay stuck on.</summary>
    private void ReleaseFire()
    {
        if (_firing && _lastCraft is { } craft)
            RaiseNetworkEvent(new FlyingCraftSetFiringEvent { Craft = craft, Firing = false });

        _firing = false;
        _lastAim = null;
    }

    /// <summary>Release fire, brake AND turn when we stop piloting, so nothing stays stuck on an empty craft.</summary>
    private void ReleaseAll()
    {
        ReleaseFire();

        if (_lastCraft is { } craft)
        {
            if (_braking)
                RaiseNetworkEvent(new FlyingCraftSetBrakingEvent { Craft = craft, Braking = false });
            if (_turn != 0)
                RaiseNetworkEvent(new FlyingCraftSetTurnEvent { Craft = craft, Turn = 0 });
        }

        _braking = false;
        _turn = 0;
        _lastCraft = null;
    }
}
