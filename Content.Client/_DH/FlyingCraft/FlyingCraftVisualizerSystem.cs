// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Robust.Client.GameObjects;

namespace Content.Client._DH.FlyingCraft;

/// <summary>
/// Swaps the craft hull sprite between the idle "base" frame and the looping "fly" animation based on the
/// networked <see cref="FlyingCraftVisuals.Flying"/> flag (set server-side from movement). The placeholder art
/// is one full-craft frame per state, so this state-swaps the single hull layer rather than overlaying.
/// (The 6-frame "shoot" state is authored in the RSI for a later weapon-fire hook.)
/// </summary>
public sealed class FlyingCraftVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(EntityUid uid, FlyingCraftComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_sprite.LayerMapTryGet((uid, args.Sprite), FlyingCraftVisualLayers.Hull, out var layer, false))
            return;

        var flying = _appearance.TryGetData<bool>(uid, FlyingCraftVisuals.Flying, out var f, args.Component) && f;

        _sprite.LayerSetRsiState((uid, args.Sprite), layer, flying ? "fly" : "base");
        _sprite.LayerSetAutoAnimated((uid, args.Sprite), layer, flying);
    }
}
