// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._DH.FlyingCraft.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._DH.FlyingCraft.UI;

/// <summary>
/// The flying-craft cockpit radar, centered on the craft every frame. Subclasses the shuttle nav radar but flips
/// <see cref="AllowResize"/>/<see cref="ScaleWithControlSize"/> so it fits the HUD panel, drives itself from
/// <see cref="FlyingCraftPilotComponent"/> (no shuttle console / BUI), zooms with the mouse wheel (inherited), and
/// has a long-range MAP mode in which a click picks the FTL/BSS jump destination (<see cref="OnDestinationPicked"/>).
/// </summary>
public sealed class FlyingCraftMiniRadarControl : ShuttleNavControl
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly SharedTransformSystem _xform;

    private const float NearRange = 80f;
    private const float MapRange = 480f;

    protected override bool AllowResize => true;
    protected override bool ScaleWithControlSize => true;
    protected override bool ShowRadarPositionMarker => true; // green marker for the craft at the centre

    /// <summary>True while the radar is zoomed out to the long-range MAP view (where a click picks an FTL target).</summary>
    public bool MapMode { get; private set; }

    /// <summary>Raised with a world location when the pilot clicks the radar in MAP mode (FTL destination).</summary>
    public Action<EntityCoordinates>? OnDestinationPicked;

    public FlyingCraftMiniRadarControl() : base(16f, 512f, NearRange)
    {
        IoCManager.InjectDependencies(this);
        _xform = EntManager.System<SharedTransformSystem>();

        MinSize = new Vector2(220, 220);
        SetSize = new Vector2(220, 220);

        // No console -> no detection gating / no server blip request; trim noise.
        SetConsole(null);
        ShowDocks = false;
        ShowIFFShuttles = true;
        HideCoords = true;

        // Stop = receive mouse wheel (inherited zoom) + clicks (MAP-mode destination pick).
        MouseFilter = MouseFilterMode.Stop;
        OnRadarClick += OnClick;
    }

    /// <summary>Toggle the long-range MAP view (zoom out + enable destination clicks).</summary>
    public void SetMapMode(bool on)
    {
        MapMode = on;
        ActualRadarRange = on ? MapRange : NearRange; // Draw() lerps WorldRange toward this each frame
    }

    private void OnClick(EntityCoordinates coords)
    {
        // The inherited handlers fire on press, while-held and release; only act in MAP mode.
        if (MapMode)
            OnDestinationPicked?.Invoke(coords);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_player.LocalEntity is not { } player
            || !EntManager.TryGetComponent<FlyingCraftPilotComponent>(player, out var pilot)
            || !EntManager.TryGetComponent<TransformComponent>(pilot.Craft, out var xform)
            || xform.MapID == MapId.Nullspace)
        {
            SetMatrix(null, null);
            return;
        }

        SetMatrix(new EntityCoordinates(pilot.Craft, Vector2.Zero), _xform.GetWorldRotation(xform));
    }
}
