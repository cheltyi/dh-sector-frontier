// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._DH.FlyingCraft;

/// <summary>
/// The role/class of a flying craft. Class sets the broad combat role; <see cref="FlyingCraftComponent.Tier"/>
/// scales the stats within a class. Placeholder sprite is shared by all classes for now.
/// </summary>
[Serializable, NetSerializable]
public enum FlyingCraftClass : byte
{
    /// <summary>Balanced all-rounder dogfighter.</summary>
    Fighter,

    /// <summary>Fastest, lightly armored hit-and-run craft.</summary>
    Interceptor,

    /// <summary>Slow, heavy, ballistic strike craft (bombs/torpedoes).</summary>
    Bomber,

    /// <summary>Durable suppression/ground-attack craft (штурмовик).</summary>
    Attacker,

    /// <summary>Fast, fragile recon craft with large sensors (разведчик).</summary>
    Scout,
}
