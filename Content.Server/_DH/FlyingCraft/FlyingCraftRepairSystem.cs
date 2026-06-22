// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using Content.Shared._DH.FlyingCraft.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;

namespace Content.Server._DH.FlyingCraft;

/// <summary>
/// Repair for flying craft: open the technical panel with a screwdriver, then insert component stacks (steel +
/// cables). Inserting the full tier bill heals +100 hull and resets for the next repair. Examining the craft with
/// the panel open shows what's still needed. Flight is blocked while the panel is open (handled in FlyingCraftSystem).
/// </summary>
public sealed class FlyingCraftRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const string Screwing = "Screwing";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlyingCraftRepairComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FlyingCraftRepairComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<FlyingCraftRepairComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Screwdriver toggles the technical panel.
        if (HasComp<ToolComponent>(args.Used) && _tool.HasQuality(args.Used, Screwing))
        {
            ent.Comp.PanelOpen = !ent.Comp.PanelOpen;
            ent.Comp.Inserted.Clear(); // any partial repair is lost when the panel state changes
            Dirty(ent);
            _popup.PopupEntity(
                Loc.GetString(ent.Comp.PanelOpen ? "flying-craft-panel-open" : "flying-craft-panel-closed"),
                ent, args.User);
            args.Handled = true;
            return;
        }

        // While the panel is open, inserting component stacks repairs the hull.
        if (ent.Comp.PanelOpen && TryComp<StackComponent>(args.Used, out var stack))
            args.Handled = TryInsertComponent(ent, args.User, (args.Used, stack));
    }

    private bool TryInsertComponent(Entity<FlyingCraftRepairComponent> ent, EntityUid user, Entity<StackComponent> stack)
    {
        var bill = GetCost(GetTier(ent));
        if (!bill.TryGetValue(stack.Comp.StackTypeId, out var needTotal))
            return false; // not a component this repair uses

        var already = ent.Comp.Inserted.GetValueOrDefault(stack.Comp.StackTypeId);
        var stillNeed = needTotal - already;
        if (stillNeed <= 0)
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-repair-enough"), ent, user);
            return true;
        }

        var take = Math.Min(stillNeed, stack.Comp.Count);
        if (take <= 0)
            return false;

        _stack.Use(stack, take, stack.Comp);
        ent.Comp.Inserted[stack.Comp.StackTypeId] = already + take;

        if (IsComplete(bill, ent.Comp.Inserted))
        {
            Heal(ent);
            ent.Comp.Inserted.Clear();
            _popup.PopupEntity(Loc.GetString("flying-craft-repair-done"), ent, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("flying-craft-repair-inserted"), ent, user);
        }

        return true;
    }

    private void OnExamined(Entity<FlyingCraftRepairComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.PanelOpen)
            return;

        var bill = GetCost(GetTier(ent));
        var parts = new List<string>();
        foreach (var (type, count) in bill)
        {
            var remaining = count - ent.Comp.Inserted.GetValueOrDefault(type);
            if (remaining > 0)
                parts.Add($"{remaining} {Loc.GetString(MaterialLocKey(type))}");
        }

        args.PushMarkup(parts.Count > 0
            ? Loc.GetString("flying-craft-repair-needed", ("list", string.Join(", ", parts)))
            : Loc.GetString("flying-craft-repair-ready"));
    }

    private void Heal(Entity<FlyingCraftRepairComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        var total = damageable.TotalDamage.Float();
        var heal = MathF.Min(ent.Comp.HealAmount, total);
        if (total > 0f && heal > 0f)
            _damageable.TryChangeDamage(ent, damageable.Damage * -(heal / total), true, false, damageable);
    }

    private static bool IsComplete(Dictionary<string, int> bill, Dictionary<string, int> inserted)
    {
        foreach (var (type, count) in bill)
        {
            if (inserted.GetValueOrDefault(type) < count)
                return false;
        }

        return true;
    }

    private int GetTier(EntityUid craft)
        => TryComp<FlyingCraftComponent>(craft, out var fc) ? fc.Tier : 1;

    /// <summary>The per-tier repair bill, keyed by stack type id (Steel / Cable=LV / CableMV / CableHV).</summary>
    private static Dictionary<string, int> GetCost(int tier) => tier switch
    {
        <= 1 => new() { ["Steel"] = 10, ["Cable"] = 5 },
        2 => new() { ["Steel"] = 15, ["Cable"] = 5, ["CableMV"] = 2 },
        3 => new() { ["Steel"] = 20, ["CableMV"] = 5, ["CableHV"] = 5 },
        4 => new() { ["Steel"] = 25, ["CableHV"] = 10 },
        _ => new() { ["Steel"] = 30, ["CableHV"] = 15 },
    };

    private static string MaterialLocKey(string stackType) => stackType switch
    {
        "Steel" => "flying-craft-mat-steel",
        "Cable" => "flying-craft-mat-lv",
        "CableMV" => "flying-craft-mat-mv",
        "CableHV" => "flying-craft-mat-hv",
        _ => "flying-craft-mat-steel",
    };
}
