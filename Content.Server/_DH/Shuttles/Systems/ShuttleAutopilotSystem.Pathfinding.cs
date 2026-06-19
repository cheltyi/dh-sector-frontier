// Dark Haven - This file is licensed under AGPLv3
// Copyright (c) 2026 Dark Haven Contributors
// See AGPLv3.txt for details.

using System.Linq;
using System.Numerics;
using Content.Shared._DH.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DH.Shuttles.Systems;

// Open-space A* navigation for shuttle-sized objects. The engine's NPC pathfinder is grid-tile only
// (mobs walking on one grid) and unusable in the void between grids, so this builds an ad-hoc uniform
// grid over the region between the ship and the goal, where a cell is blocked if the ship (treated as a
// disc of its bounding radius + clearance) would overlap any other grid's tiles there. That makes the
// path width-aware: it routes around asteroid fields and through gaps the ship actually fits.
public sealed partial class ShuttleAutopilotSystem
{
    private const float PathClearance = 2.5f;     // extra margin beyond the ship's half-diagonal
    private const float PathMinCell = 4f;          // smallest A* cell size (world units) — finer = tighter routing
    private const float PathMaxAxisCells = 110f;   // cap cells per axis (bounds search cost)
    private const float PathDetourPad = 96f;       // world-distance room around the direct line to route around obstacles
    private const int PathMaxCells = 24000;        // hard cap on total cells (defends against huge regions)
    private const float LongHaulDistance = 450f;   // beyond fine A* reach: fall back to a direct heading (re-planned on approach)

    private List<Entity<MapGridComponent>> _pathGrids = new();

    // Permeability for the current search (set per TryFindPath call; pathfinding is synchronous). The target
    // grid is a normal obstacle EXCEPT within a disc around the dock, so we route around the hull and only the
    // dock approach is open — instead of letting A* cut straight through the whole station.
    private EntityUid? _permeableGrid;
    private Vector2 _permeableCenter;
    private float _permeableRadius;

    // 8-connected neighbour offsets: 4 orthogonal then 4 diagonal.
    private static readonly int[] NeighborDx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] NeighborDy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    /// <summary>
    /// (Re)computes <see cref="ShuttleAutopilotComponent.Route"/> from the current ship position to the
    /// stored target/dock. Tier 1 produces a straight line; tier 2/3 run A*. Returns false (with a
    /// localized <paramref name="reason"/> key) if no usable route exists.
    /// </summary>
    private bool TryBuildRoute(EntityUid grid, ShuttleAutopilotComponent ap, out string reason)
    {
        reason = string.Empty;
        ap.Route.Clear();
        ap.WaypointIndex = 0;

        var xform = Transform(grid);
        var mapId = xform.MapID;
        if (mapId == MapId.Nullspace)
        {
            reason = "autopilot-popup-fail-nomap";
            return false;
        }

        var shipPos = GetShipCenter(grid, xform);

        Vector2 goal;
        EntityUid? permeableGrid = null;
        var permeableCenter = Vector2.Zero;
        var permeableRadius = 0f;

        if (ap.Tier >= 3 && ap.TargetDock is { } dockNet)
        {
            var dockUid = GetEntity(dockNet);
            if (Deleted(dockUid))
            {
                reason = "autopilot-popup-fail-nodock";
                return false;
            }

            var tg = Transform(dockUid).GridUid;
            if (tg == null)
            {
                reason = "autopilot-popup-fail-nodock";
                return false;
            }

            // Route to a STANDOFF point off the dock along its approach axis. Derived from the dock itself
            // (not the full docking config), so the route + marker ALWAYS build even if the precise fit can't
            // be solved yet; DriveDock handles the final slide-in and latch.
            var dockWorld = _xform.GetWorldPosition(dockUid);
            goal = dockWorld + DockApproachAxis(tg.Value, dockUid, dockWorld) * DockStandoffDistance;
            // Make only a disc around the dock permeable (not the whole station), so A* routes around the hull
            // and approaches through the dock opening instead of cutting through the far side of the station.
            permeableGrid = tg.Value;
            permeableCenter = dockWorld;
            permeableRadius = DockStandoffDistance + GetShipRadius(grid) + 6f;

            // Best-effort: compute & cache the docking config now (while far, so the fit check passes) for the
            // latch later. Not required for the route to build.
            TryResolveDock(grid, ap, recompute: true, out _, out _, out _, out _);
        }
        else if (ap.Target is { } targetNet)
        {
            goal = _xform.ToMapCoordinates(GetCoordinates(targetNet)).Position;
        }
        else
        {
            reason = "autopilot-popup-no-route";
            return false;
        }

        if (ap.Tier <= 1)
        {
            ap.Route.Add(goal);
            return true;
        }

        if (TryFindPath(grid, mapId, shipPos, goal, permeableGrid, permeableCenter, permeableRadius, out var path))
        {
            ap.Route.AddRange(path);
            return true;
        }

        // Docking: even if A* can't find a clear lane to the standoff, still head there directly so the route
        // and the dock marker show and the ship approaches (DriveDock + collision handle the close part).
        if (ap.TargetDock != null)
        {
            ap.Route.Add(goal);
            return true;
        }

        // Far open-space haul — typically a point picked on the big sector map, well beyond the practical
        // reach of fine-grained A*. Head straight toward it: obstacle avoidance matters near structures, but
        // over a long open distance a direct heading is fine, and for tier >= 2 the periodic/stuck re-plan
        // re-routes with fine A* once we close in on anything in the way.
        if ((goal - shipPos).Length() > LongHaulDistance)
        {
            ap.Route.Add(goal);
            return true;
        }

        reason = "autopilot-popup-fail-nopath";
        return false;
    }

    private float GetShipRadius(EntityUid grid)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return 4f;

        return 0.5f * gridComp.LocalAABB.Size.Length() + PathClearance;
    }

    private bool TryFindPath(EntityUid grid, MapId mapId, Vector2 start, Vector2 goal,
        EntityUid? permeableGrid, Vector2 permeableCenter, float permeableRadius, out List<Vector2> path)
    {
        path = new List<Vector2>();
        var radius = GetShipRadius(grid);

        _permeableGrid = permeableGrid;
        _permeableCenter = permeableCenter;
        _permeableRadius = permeableRadius;

        // The region must enclose enough room to route AROUND the obstacles crossing the start->goal corridor,
        // not just a fixed pad — otherwise a point behind a big station leaves no room to detour. Union in the
        // world AABBs of grids near the corridor, then pad.
        var region = new Box2(Vector2.Min(start, goal), Vector2.Max(start, goal)).Enlarged(radius);
        var scanGrids = new List<Entity<MapGridComponent>>();
        _mapManager.FindGridsIntersecting(mapId, region.Enlarged(PathDetourPad), ref scanGrids, includeMap: false);
        foreach (var g in scanGrids)
        {
            if (g.Owner == grid)
                continue;
            region = region.Union(_xform.GetWorldMatrix(g.Owner).TransformBox(g.Comp.LocalAABB));
        }
        region = region.Enlarged(PathDetourPad + radius);

        var span = MathF.Max(region.Width, region.Height);
        // Cell count per axis is capped (span/cell <= PathMaxAxisCells) while staying at least ship-sized.
        var cell = MathF.Max(MathF.Max(PathMinCell, radius), span / PathMaxAxisCells);

        // Cover a full cell so thin walls between cell centres aren't skipped, but keep it modest so we don't
        // false-block gaps the ship actually fits through.
        var testRadius = MathF.Max(radius, cell * 0.5f);

        var gw = Math.Max(1, (int)MathF.Ceiling(MathF.Max(region.Width, cell) / cell));
        var gh = Math.Max(1, (int)MathF.Ceiling(MathF.Max(region.Height, cell) / cell));
        var origin = region.BottomLeft;
        var total = gw * gh;

        // Hard safety cap against a degenerate/huge grid (the target distance arrives via an unvalidated message).
        if (total <= 0 || total > PathMaxCells)
            return false;

        int CellX(Vector2 p) => Math.Clamp((int)((p.X - origin.X) / cell), 0, gw - 1);
        int CellY(Vector2 p) => Math.Clamp((int)((p.Y - origin.Y) / cell), 0, gh - 1);
        Vector2 CellCenter(int x, int y) => new(origin.X + (x + 0.5f) * cell, origin.Y + (y + 0.5f) * cell);

        var memo = new sbyte[total]; // 0 unknown, 1 free, 2 blocked
        bool IsBlocked(int x, int y)
        {
            var idx = y * gw + x;
            if (memo[idx] != 0)
                return memo[idx] == 2;

            var blocked = IsBoxBlocked(grid, mapId, CellCenter(x, y), testRadius);
            memo[idx] = (sbyte)(blocked ? 2 : 1);
            return blocked;
        }

        var startX = CellX(start);
        var startY = CellY(start);
        var goalX = CellX(goal);
        var goalY = CellY(goal);

        var relocated = false;
        if (IsBlocked(goalX, goalY))
        {
            if (!TryFindNearestFree(goalX, goalY, gw, gh, IsBlocked, out goalX, out goalY))
                return false;
            relocated = true;
        }

        var startIdx = startY * gw + startX;
        var goalIdx = goalY * gw + goalX;

        var gScore = new float[total];
        Array.Fill(gScore, float.MaxValue);
        var came = new int[total];
        Array.Fill(came, -1);
        var closed = new bool[total];

        gScore[startIdx] = 0f;
        var open = new PriorityQueue<int, float>();
        open.Enqueue(startIdx, Heuristic(startX, startY, goalX, goalY));

        var dx = NeighborDx;
        var dy = NeighborDy;

        var found = false;
        var expansions = 0;
        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == goalIdx)
            {
                found = true;
                break;
            }

            if (closed[current])
                continue; // stale duplicate from lazy-deletion; don't count against the budget
            closed[current] = true;
            if (++expansions > total)
                break;

            var cx = current % gw;
            var cy = current / gw;

            for (var i = 0; i < 8; i++)
            {
                var nx = cx + dx[i];
                var ny = cy + dy[i];
                if (nx < 0 || ny < 0 || nx >= gw || ny >= gh)
                    continue;

                var nIdx = ny * gw + nx;
                // The start cell may register as blocked (ship overlaps its own clearance margin); allow leaving it.
                if (nIdx != startIdx && IsBlocked(nx, ny))
                    continue;

                var diagonal = i >= 4;
                if (diagonal && (IsBlocked(cx + dx[i], cy) || IsBlocked(cx, cy + dy[i])))
                    continue; // no corner cutting -> respects ship width

                var tentative = gScore[current] + (diagonal ? 1.41421356f : 1f);
                if (tentative >= gScore[nIdx])
                    continue;

                came[nIdx] = current;
                gScore[nIdx] = tentative;
                open.Enqueue(nIdx, tentative + Heuristic(nx, ny, goalX, goalY));
            }
        }

        if (!found)
            return false;

        // Reconstruct cell path, then convert to world points (exact goal for the last point).
        var cells = new List<int>();
        var node = goalIdx;
        while (node != -1)
        {
            cells.Add(node);
            if (node == startIdx)
                break;
            node = came[node];
        }
        cells.Reverse();

        var pts = new List<Vector2>(cells.Count);
        foreach (var c in cells)
            pts.Add(CellCenter(c % gw, c / gw));
        if (pts.Count > 0)
        {
            // Snap to the exact target only when it was actually free; if we relocated the goal to a nearby
            // free cell (e.g. the player clicked inside an asteroid), keep that cell so we don't ram the rock.
            if (!relocated)
                pts[^1] = goal;
        }
        else
        {
            pts.Add(relocated ? CellCenter(goalX, goalY) : goal);
        }

        path = SmoothPath(grid, mapId, start, pts, testRadius, cell);
        return path.Count > 0;
    }

    private static float Heuristic(int x, int y, int gx, int gy)
    {
        float ddx = x - gx;
        float ddy = y - gy;
        return MathF.Sqrt(ddx * ddx + ddy * ddy);
    }

    private static bool TryFindNearestFree(int gx, int gy, int gw, int gh, Func<int, int, bool> isBlocked, out int fx, out int fy)
    {
        fx = gx;
        fy = gy;
        var maxR = Math.Max(gw, gh);
        for (var r = 1; r <= maxR; r++)
        {
            for (var oy = -r; oy <= r; oy++)
            {
                for (var ox = -r; ox <= r; ox++)
                {
                    if (Math.Abs(ox) != r && Math.Abs(oy) != r)
                        continue; // ring perimeter only

                    var nx = gx + ox;
                    var ny = gy + oy;
                    if (nx < 0 || ny < 0 || nx >= gw || ny >= gh)
                        continue;
                    if (!isBlocked(nx, ny))
                    {
                        fx = nx;
                        fy = ny;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>String-pulling: collapse the cell path into long straight legs where the corridor is clear.</summary>
    private List<Vector2> SmoothPath(EntityUid grid, MapId mapId, Vector2 start, List<Vector2> pts, float radius, float cell)
    {
        var result = new List<Vector2>();
        if (pts.Count == 0)
            return result;

        var anchor = start;
        var i = 0;
        while (i < pts.Count)
        {
            var j = i;
            for (var k = pts.Count - 1; k >= i; k--)
            {
                if (IsLineClear(grid, mapId, anchor, pts[k], radius, cell))
                {
                    j = k;
                    break;
                }
            }

            result.Add(pts[j]);
            anchor = pts[j];
            i = j + 1;
        }

        if (result.Count == 0 || result[^1] != pts[^1])
            result.Add(pts[^1]);

        return result;
    }

    private bool IsLineClear(EntityUid grid, MapId mapId, Vector2 a, Vector2 b, float radius, float cell)
    {
        var delta = b - a;
        var dist = delta.Length();
        if (dist < 0.01f)
            return true;

        var steps = Math.Max(1, (int)MathF.Ceiling(dist / (cell * 0.5f)));
        for (var s = 1; s < steps; s++)
        {
            var p = a + delta * (s / (float)steps);
            if (IsBoxBlocked(grid, mapId, p, radius))
                return false;
        }

        return true;
    }

    /// <summary>
    /// True if the ship (a square of half-extent <paramref name="radius"/>) centered at
    /// <paramref name="worldPos"/> would overlap any other grid's tiles. Tile-accurate so the ship can
    /// thread gaps between separate asteroid grids.
    /// </summary>
    private bool IsBoxBlocked(EntityUid grid, MapId mapId, Vector2 worldPos, float radius)
    {
        var box = new Box2(worldPos - new Vector2(radius, radius), worldPos + new Vector2(radius, radius));
        _pathGrids.Clear();
        // includeMap:false — don't treat the map entity's own grid (planet/biome maps) as a solid obstacle.
        _mapManager.FindGridsIntersecting(mapId, box, ref _pathGrids, includeMap: false);

        foreach (var g in _pathGrids)
        {
            if (g.Owner == grid)
                continue;

            // The dock's target grid is permeable ONLY within a disc around the dock, so we can approach the
            // port; elsewhere it's a normal obstacle to route around (no cutting through the far side).
            if (_permeableGrid != null && g.Owner == _permeableGrid.Value
                && (worldPos - _permeableCenter).Length() <= _permeableRadius)
                continue;

            var local = _xform.GetInvWorldMatrix(g.Owner).TransformBox(box);
            if (_map.GetLocalTilesIntersecting(g.Owner, g.Comp, local).Any())
                return true;
        }

        return false;
    }
}
