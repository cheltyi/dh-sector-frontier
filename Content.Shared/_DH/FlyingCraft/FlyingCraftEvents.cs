// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._DH.FlyingCraft;

/// <summary>DoAfter completion: the dragged mob finished climbing into the cockpit. Raised on the craft.</summary>
[Serializable, NetSerializable]
public sealed partial class FlyingCraftEntryEvent : SimpleDoAfterEvent
{
}

/// <summary>DoAfter completion: the pilot finished climbing OUT of the cockpit (interrupted if the craft moves).</summary>
[Serializable, NetSerializable]
public sealed partial class FlyingCraftExitDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>Cycles the craft's active weapon (relayed from the pilot's action to the craft).</summary>
public sealed partial class FlyingCraftSwapWeaponActionEvent : InstantActionEvent
{
}

/// <summary>Toggles the craft's white headlight (relayed from the pilot's action to the craft).</summary>
public sealed partial class FlyingCraftHeadlightActionEvent : InstantActionEvent
{
}

/// <summary>
/// Client -> server: the pilot started/stopped holding fire (LMB) in combat mode. Sent only when the held state
/// flips. The server validates the sender is the craft's seated pilot before toggling the active weapon's fire.
/// </summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftSetFiringEvent : EntityEventArgs
{
    public NetEntity Craft;
    public bool Firing;
}

/// <summary>
/// Client -> server: the pilot's desired aim (the craft's body rotation so its nose points at the cursor) in
/// combat mode. Sent when the cursor moves beyond a tolerance. The flight controller turns toward it with capped
/// torque (never SetWorldRotation, which would fight the Dynamic body).
/// </summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftSetAimEvent : EntityEventArgs
{
    public NetEntity Craft;
    public Angle Rotation;
}

/// <summary>
/// Client -> server: the pilot started/stopped holding the brake (Space). Sent only when the held state flips. The
/// flight controller bleeds the craft's linear velocity toward zero while braking.
/// </summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftSetBrakingEvent : EntityEventArgs
{
    public NetEntity Craft;
    public bool Braking;
}

/// <summary>
/// Client -> server: the pilot's manual rotate input (Q/E), -1 (left/CCW), 0, or +1 (right/CW). Sent only when the
/// held state flips. Used as the manual turn outside combat mode (combat-mode cursor auto-turn takes priority).
/// </summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftSetTurnEvent : EntityEventArgs
{
    public NetEntity Craft;
    public int Turn;
}

/// <summary>
/// Client -> server: the pilot clicked an FTL/BSS destination on the radar MAP. The server validates the sender is
/// the craft's seated pilot, then starts the per-tier charge DoAfter.
/// </summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftBeginFtlEvent : EntityEventArgs
{
    public NetEntity Craft;
    public NetCoordinates Destination;
}

/// <summary>DoAfter charge for the FTL/BSS jump; carries the destination. Raised on the craft when it completes.</summary>
[Serializable, NetSerializable]
public sealed partial class FlyingCraftFtlChargeEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Destination { get; private set; }

    private FlyingCraftFtlChargeEvent()
    {
    }

    public FlyingCraftFtlChargeEvent(NetCoordinates destination)
    {
        Destination = destination;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>The cockpit HUD on-screen control buttons (relayed to the server, validated against the seated pilot).</summary>
public enum FlyingCraftHudButton : byte
{
    Exit,
    SwapWeapon,
    Headlight,
}

/// <summary>Client -> server: the pilot pressed a cockpit-HUD control button.</summary>
[Serializable, NetSerializable]
public sealed class FlyingCraftHudButtonEvent : EntityEventArgs
{
    public NetEntity Craft;
    public FlyingCraftHudButton Button;
}
