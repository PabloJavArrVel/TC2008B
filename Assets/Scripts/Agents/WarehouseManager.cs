using System;
using System.Collections.Generic;
using System.Linq;

public class WarehouseManager
{
    private Warehouse warehouse;

    private Queue<Crate> unassignedCrates = new();
    private Queue<Cell> dropZones = new();

    private Dictionary<RobotAgent, bool> robotCarrying = new();

    // =========================
    // CONSTRUCTOR
    // =========================

    public WarehouseManager(Warehouse warehouse)
    {
        this.warehouse = warehouse;

        InitializeDropZones(warehouse.Crates.Count);
        InitializeCrateQueue();

        foreach (var robot in warehouse.Robots)
        {
            robotCarrying[robot] = false;
            robot.OnTaskFinished += HandleRobotFree;
        }

        // give initial tasks
        foreach (var robot in warehouse.Robots)
            AssignNextTask(robot);
    }

    // =========================
    // INITIALIZE CRATE QUEUE
    // =========================

    void InitializeCrateQueue()
    {
        foreach (var crate in warehouse.Crates)
            unassignedCrates.Enqueue(crate);
    }

    // =====================================================
    // QUADRANT DROP ZONE INITIALIZATION 
    // =====================================================

    void InitializeDropZones(int crateCount)
    {
        dropZones.Clear();

        int stackCount = crateCount / 5; // full stacks only
        if (stackCount == 0)
            return;

        // distribute stacks across 4 quadrants
        int[] perQuadrant = DistributeStacks(stackCount);

        // get quadrant centers
        var centers = GetQuadrantCenters();

        // generate zones for each quadrant
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

    // find center of each quadrant
    List<(int row, int col)> GetQuadrantCenters()
    {
        int midR = warehouse.Rows / 2;
        int midC = warehouse.Cols / 2;

        return new List<(int, int)>
        {
            (midR / 2, midC / 2),                                  // top-left
            (midR / 2, midC + (warehouse.Cols - midC) / 2),        // top-right
            (midR + (warehouse.Rows - midR) / 2, midC / 2),        // bottom-left
            (midR + (warehouse.Rows - midR) / 2,
             midC + (warehouse.Cols - midC) / 2)                   // bottom-right
        };
    }

    // generate cells around center using expanding radius
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

                // ignore shelves (future-proof)
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
        if (!robotCarrying[robot])
            AssignPickup(robot);
        else
            AssignDrop(robot);
    }

    void AssignPickup(RobotAgent robot)
    {
        if (unassignedCrates.Count == 0)
            return;

        var crate = unassignedCrates.Dequeue();

        var path = ComputePath(robot.CurrentCell, crate.CurrentCell);

        robot.AssignPath(path);
        robotCarrying[robot] = true;
    }

    void AssignDrop(RobotAgent robot)
    {
        if (dropZones.Count == 0)
            return;

        int attempts = dropZones.Count;

        while (attempts-- > 0)
        {
            var zone = dropZones.Dequeue();
            dropZones.Enqueue(zone); // rotate

            // skip full stacks
            if (!zone.CanStack())
                continue;

            var path = ComputePath(robot.CurrentCell, zone);
            robot.AssignPath(path);
            return;
        }

    }

    // =========================
    // GRID PATHFINDING (BFS)
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