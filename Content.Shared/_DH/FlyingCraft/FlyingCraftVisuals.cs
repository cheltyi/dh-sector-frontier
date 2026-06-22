// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._DH.FlyingCraft;

/// <summary>
/// Appearance keys the client visualizer reads to swap the craft hull sprite state between idle (base),
/// flight (looping "fly" frames) and firing (one-shot "shoot" flick).
/// </summary>
[Serializable, NetSerializable]
public enum FlyingCraftVisuals : byte
{
    /// <summary>True while the craft is actively moving under thrust (plays the looping fly animation).</summary>
    Flying,
}

/// <summary>Sprite layer keys for the craft. The single hull layer is state-swapped by the visualizer.</summary>
[Serializable, NetSerializable]
public enum FlyingCraftVisualLayers : byte
{
    Hull,
}
