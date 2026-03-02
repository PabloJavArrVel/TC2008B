using System;
using System.Collections.Generic;
using System.Linq;

public class WarehouseManager
{
    private Warehouse warehouse;

    private Queue<Crate> unassignedCrates = new();
    private Queue<Cell> dropZones = new();

    // =========================
    // CONSTRUCTOR
    // =========================

    public WarehouseManager(Warehouse warehouse)
    {
        this.warehouse = warehouse;

        InitializeDropZones(warehouse.Crates.Count);
        InitializeCrateQueue();

        foreach (var robot in warehouse.Robots)
            robot.OnTaskFinished += HandleRobotFree;

        foreach (var robot in warehouse.Robots)
            AssignNextTask(robot);
    }

    // =========================
    // INITIALIZE CRATES
    // =========================

    void InitializeCrateQueue()
    {
        foreach (var crate in warehouse.Crates)
            unassignedCrates.Enqueue(crate);
    }

    // =========================
    // ROBOT FINISHED TASK
    // =========================

    void HandleRobotFree(RobotAgent robot)
    {
        AssignNextTask(robot);
    }

    // =========================
    // TASK ASSIGNMENT
    // =========================

    void AssignNextTask(RobotAgent robot)
    {
        if (!robot.IsCarrying)
            AssignPickup(robot);
        else
            AssignDrop(robot);
    }

    // =========================
    // PICKUP ASSIGNMENT
    // =========================

    void AssignPickup(RobotAgent robot)
    {
        // remove invalid crates (already picked)
        while (unassignedCrates.Count > 0)
        {
            var crate = unassignedCrates.Peek();

            if (crate.CurrentCell.OccupyingCrate == crate)
                break;

            unassignedCrates.Dequeue();
        }

        if (unassignedCrates.Count == 0)
            return;

        var target = unassignedCrates.Dequeue();

        var path = ComputePath(robot.CurrentCell, target.CurrentCell);
        robot.AssignPath(path);
    }

    // =========================
    // DROP ASSIGNMENT
    // =========================

    void AssignDrop(RobotAgent robot)
    {
        if (dropZones.Count == 0)
            return;

        int attempts = dropZones.Count;

        while (attempts-- > 0)
        {
            var zone = dropZones.Dequeue();
            dropZones.Enqueue(zone);

            if (!zone.CanStack())
                continue;

            var path = ComputePath(robot.CurrentCell, zone);
            robot.AssignPath(path);
            return;
        }
    }

    // =====================================================
    // DROP ZONE INITIALIZATION
    // =====================================================

    void InitializeDropZones(int crateCount)
    {
        dropZones.Clear();

        int stackCount = crateCount / 5;
        if (stackCount == 0)
            return;

        int[] perQuadrant = DistributeStacks(stackCount);
        var centers = GetQuadrantCenters();

        for (int q = 0; q < 4; q++)
        {
            var zones = GenerateZonesAround(
                centers[q].row,
                centers[q].col,
                perQuadrant[q]
            );

            foreach (var z in zones)
            {
                z.IsDropZone = true;
                dropZones.Enqueue(z);
            }
        }
    }

    int[] DistributeStacks(int total)
    {
        int[] result = new int[4];

        for (int i = 0; i < total; i++)
            result[i % 4]++;

        return result;
    }

    List<(int row, int col)> GetQuadrantCenters()
    {
        int midR = warehouse.Rows / 2;
        int midC = warehouse.Cols / 2;

        return new List<(int, int)>
        {
            (midR / 2, midC / 2),
            (midR / 2, midC + (warehouse.Cols - midC) / 2),
            (midR + (warehouse.Rows - midR) / 2, midC / 2),
            (midR + (warehouse.Rows - midR) / 2,
             midC + (warehouse.Cols - midC) / 2)
        };
    }

    List<Cell> GenerateZonesAround(int centerR, int centerC, int needed)
    {
        var result = new List<Cell>();
        int radius = 0;

        while (result.Count < needed)
        {
            for (int r = centerR - radius; r <= centerR + radius; r++)
            for (int c = centerC - radius; c <= centerC + radius; c++)
            {
                if (result.Count >= needed)
                    break;

                if (!InBounds(r, c))
                    continue;

                var cell = warehouse.Grid[r, c];

                if (!cell.IsShelf && !result.Contains(cell))
                    result.Add(cell);
            }

            radius++;
        }

        return result;
    }

    bool InBounds(int r, int c)
    {
        return r >= 0 &&
               c >= 0 &&
               r < warehouse.Rows &&
               c < warehouse.Cols;
    }

    // =========================
    // BFS PATHFINDING
    // =========================

    Queue<Cell> ComputePath(Cell start, Cell goal)
    {
        var frontier = new Queue<Cell>();
        var cameFrom = new Dictionary<Cell, Cell>();

        frontier.Enqueue(start);
        cameFrom[start] = null;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current == goal)
                break;

            foreach (var next in warehouse.GetNeighbors(current))
            {
                if (!next.IsFree() && next != goal)
                    continue;

                if (cameFrom.ContainsKey(next))
                    continue;

                frontier.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!cameFrom.ContainsKey(goal))
            return new Queue<Cell>();

        var path = new Stack<Cell>();
        var step = goal;

        while (step != start)
        {
            path.Push(step);
            step = cameFrom[step];
        }

        return new Queue<Cell>(path);
    }
}