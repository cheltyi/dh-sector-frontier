// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

namespace Content.Shared._DH.Needs;

/// <summary>
/// Raised (directed) on a mob when it finishes eating or drinking. The bladder need listens to this so that
/// eating and (more so) drinking accelerate how fast the bladder fills.
/// </summary>
[ByRefEvent]
public readonly record struct IngestedEvent(bool IsDrink);
