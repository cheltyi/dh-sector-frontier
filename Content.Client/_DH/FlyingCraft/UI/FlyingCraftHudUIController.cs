// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared._DH.FlyingCraft;
using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.Damage;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._DH.FlyingCraft.UI;

/// <summary>
/// Owns the flying-craft cockpit <see cref="FlyingCraftHud"/>: creates it above the hotbar on screen load (open by
/// default), shows it only while the local player is the seated pilot, pushes live ammo/reload/health/fuel/speed/
/// coords into it each frame, wires the on-screen control buttons (exit / headlight / swap / MAP) to the server, and
/// gates the tier-3+ MAP/FTL button. Auto-discovered by reflection; mirrors AlertsUIController's widget lifecycle.
/// </summary>
public sealed class FlyingCraftHudUIController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private FlyingCraftHud? _hud;
    private bool _expanded = true; // open by default

    public override void Initialize()
    {
        base.Initialize();

        var load = UIManager.GetUIController<GameplayStateLoadController>();
        load.OnScreenLoad += OnScreenLoad;
        load.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        if (UIManager.ActiveScreen is not { } screen)
            return;

        var hotbar = FindChild<HotbarGui>(screen);
        if (hotbar?.AlertsContainer is not { } container)
            return;

        _hud = new FlyingCraftHud { Visible = false };
        _hud.SetExpanded(_expanded);
        _hud.ExpandButton.Pressed = _expanded;

        _hud.ExpandButton.OnToggled += OnExpandToggled;
        _hud.ExitButton.OnPressed += OnExitPressed;
        _hud.SwapWeaponButton.OnPressed += OnSwapPressed;
        _hud.HeadlightButton.OnPressed += OnHeadlightPressed;
        _hud.MapButton.OnToggled += OnMapToggled;
        _hud.MiniRadar.OnDestinationPicked += OnRadarDestination;

        container.AddChild(_hud);
    }

    private void OnScreenUnload()
    {
        if (_hud == null)
            return;

        _hud.ExpandButton.OnToggled -= OnExpandToggled;
        _hud.ExitButton.OnPressed -= OnExitPressed;
        _hud.SwapWeaponButton.OnPressed -= OnSwapPressed;
        _hud.HeadlightButton.OnPressed -= OnHeadlightPressed;
        _hud.MapButton.OnToggled -= OnMapToggled;
        _hud.MiniRadar.OnDestinationPicked -= OnRadarDestination;

        _hud.Orphan();
        _hud = null;
    }

    private void OnExpandToggled(BaseButton.ButtonToggledEventArgs args)
    {
        _expanded = args.Pressed;
        _hud?.SetExpanded(_expanded);
    }

    private void OnExitPressed(BaseButton.ButtonEventArgs _) => SendButton(FlyingCraftHudButton.Exit);
    private void OnSwapPressed(BaseButton.ButtonEventArgs _) => SendButton(FlyingCraftHudButton.SwapWeapon);
    private void OnHeadlightPressed(BaseButton.ButtonEventArgs _) => SendButton(FlyingCraftHudButton.Headlight);

    private void OnMapToggled(BaseButton.ButtonToggledEventArgs args)
    {
        _hud?.MiniRadar.SetMapMode(args.Pressed);
    }

    private void OnRadarDestination(EntityCoordinates coords)
    {
        if (_hud == null
            || _player.LocalEntity is not { } player
            || !EntityManager.TryGetComponent<FlyingCraftPilotComponent>(player, out var pilot))
            return;

        EntityManager.System<FlyingCraftCombatSystem>()
            .SendBeginFtl(EntityManager.GetNetEntity(pilot.Craft), EntityManager.GetNetCoordinates(coords));

        // One-shot: leave MAP mode so repeated click-path invokes don't re-fire.
        _hud.MapButton.Pressed = false;
        _hud.MiniRadar.SetMapMode(false);
    }

    private void SendButton(FlyingCraftHudButton button)
    {
        if (_player.LocalEntity is not { } player
            || !EntityManager.TryGetComponent<FlyingCraftPilotComponent>(player, out var pilot))
            return;

        EntityManager.System<FlyingCraftCombatSystem>()
            .SendHudButton(EntityManager.GetNetEntity(pilot.Craft), button);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_hud == null)
            return;

        // Same gate as FlyingCraftCombatSystem: show only while piloting a craft.
        if (_player.LocalEntity is not { } player
            || !EntityManager.TryGetComponent<FlyingCraftPilotComponent>(player, out var pilot)
            || !EntityManager.HasComponent<FlyingCraftComponent>(pilot.Craft))
        {
            _hud.Visible = false;
            return;
        }

        _hud.Visible = true;
        if (_hud.BodyPanel.Visible)
            UpdateHud(pilot.Craft);
    }

    private void UpdateHud(EntityUid craft)
    {
        if (_hud == null)
            return;

        EntityManager.TryGetComponent<FlyingCraftHudStateComponent>(craft, out var hud);

        // Ammo + reload of the active weapon.
        if (hud != null && hud.Ammo >= 0)
        {
            _hud.AmmoLabel.Visible = true;
            _hud.ReloadBar.Visible = true;

            var reloading = hud.ReloadEnd != TimeSpan.Zero && _timing.CurTime < hud.ReloadEnd;
            if (reloading && hud.ReloadDuration > TimeSpan.Zero)
            {
                var remaining = (hud.ReloadEnd - _timing.CurTime).TotalSeconds;
                var progress = 1f - (float) (remaining / hud.ReloadDuration.TotalSeconds);
                _hud.ReloadBar.Value = Math.Clamp(progress, 0f, 1f);
                _hud.AmmoLabel.Text = Loc.GetString("flyingcraft-hud-reloading");
            }
            else
            {
                _hud.ReloadBar.Value = 1f;
                _hud.AmmoLabel.Text = Loc.GetString("flyingcraft-hud-ammo",
                    ("count", hud.Ammo), ("max", hud.MagazineSize));
            }
        }
        else
        {
            _hud.AmmoLabel.Visible = false;
            _hud.ReloadBar.Visible = false;
        }

        // Health (hull): TotalDamage is networked; max comes from the relayed destruction threshold.
        if (hud is { MaxHealth: > 0f } && EntityManager.TryGetComponent<DamageableComponent>(craft, out var dmg))
        {
            _hud.HealthLabel.Visible = true;
            _hud.HealthBar.Visible = true;
            var frac = Math.Clamp(1f - (float) (dmg.TotalDamage.Float() / hud.MaxHealth), 0f, 1f);
            _hud.HealthBar.Value = frac;
            _hud.HealthLabel.Text = Loc.GetString("flyingcraft-hud-health", ("hp", $"{frac * 100:0}"));
        }
        else
        {
            _hud.HealthLabel.Visible = false;
            _hud.HealthBar.Visible = false;
        }

        // Fuel (percent of max).
        if (EntityManager.TryGetComponent<FlyingCraftFuelComponent>(craft, out var fuel))
        {
            _hud.FuelLabel.Visible = true;
            var pct = fuel.MaxFuel > 0f ? fuel.Fuel / fuel.MaxFuel * 100f : 0f;
            _hud.FuelLabel.Text = Loc.GetString("flyingcraft-hud-fuel", ("fuel", $"{pct:0}"));
        }
        else
        {
            _hud.FuelLabel.Visible = false;
        }

        // Speed (tiles/sec == m/s in SS14).
        if (EntityManager.TryGetComponent<PhysicsComponent>(craft, out var body))
            _hud.SpeedLabel.Text = Loc.GetString("flyingcraft-hud-speed", ("speed", $"{body.LinearVelocity.Length():0.0}"));

        // Map coordinates.
        var pos = EntityManager.System<SharedTransformSystem>().GetMapCoordinates(craft).Position;
        _hud.CoordsLabel.Text = Loc.GetString("flyingcraft-hud-coords", ("X", $"{pos.X:0.0}"), ("Y", $"{pos.Y:0.0}"));

        // MAP/FTL (BSS) is only on tier 3+. Grey the button otherwise and force the radar back to near view.
        var tier3 = EntityManager.TryGetComponent<FlyingCraftComponent>(craft, out var fc) && fc.Tier >= 3;
        _hud.MapButton.Disabled = !tier3;
        if (!tier3 && _hud.MapButton.Pressed)
        {
            _hud.MapButton.Pressed = false;
            _hud.MiniRadar.SetMapMode(false);
        }
    }

    private static T? FindChild<T>(Control root) where T : Control
    {
        if (root is T t)
            return t;

        foreach (var child in root.Children)
        {
            var found = FindChild<T>(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
