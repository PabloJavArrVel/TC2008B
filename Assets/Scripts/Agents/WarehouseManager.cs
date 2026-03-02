/*
========================================================
WAREHOUSE MANAGER — CENTRALIZED

RESPONSIBILITY:
- Perceive world state
- Assign robot tasks
- Compute paths
- Detect blocking
- Prevent deadlocks
- Replan stuck robots
- Move idle robots away from critical cells

PERCEIVE → DELIBERATE → ACT LOOP:
Tick()
  1. Perceive world
  2. Assign tasks if needed
  3. Detect blocking
  4. Replan if stuck
========================================================
*/

using System.Collections.Generic;
using System.Linq;

public class WarehouseManager
{
    Warehouse warehouse;

    Dictionary<RobotAgent, int> blockedTicks = new();

    List<Crate> unassignedCrates = new();
    Dictionary<Crate, RobotAgent> crateAssignments = new();

    HashSet<RobotAgent> parkedRobots = new();

    Dictionary<RobotAgent, Cell> parkingAssignments = new();

    const int REPLAN_THRESHOLD = 10;

    public WarehouseManager(Warehouse warehouse)
    {
        this.warehouse = warehouse;

        foreach (var robot in warehouse.Robots)
            blockedTicks[robot] = 0;
    }

    // =====================================================
    // MAIN LOOP
    // =====================================================

    public void Tick()
    {
        Perceive();
        Deliberate();
        Act();
    }

    // =====================================================
    // PERCEIVE
    // =====================================================

    void Perceive()
    {
        // Remove assignments for crates no longer in world
        crateAssignments = crateAssignments
            .Where(kv =>
                kv.Key.CurrentCell != null &&
                !kv.Key.IsDelivered)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // Release crates whose robot stopped pursuing them
        var abandoned = crateAssignments
            .Where(kv =>
                !kv.Value.IsCarrying &&
                !kv.Value.HasPath)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var crate in abandoned)
            crateAssignments.Remove(crate);

        // Rebuild unassigned crates directly from world state
        unassignedCrates = warehouse.Crates
            .Where(c =>
                c.CurrentCell != null &&
                !c.IsDelivered &&
                !crateAssignments.ContainsKey(c))
            .ToList();
    }

    // =====================================================
    // DELIBERATE
    // =====================================================

    void Deliberate()
    {
        foreach (var robot in warehouse.Robots)
        {
            EnsureRobotHasTask(robot);
        }
    }

    // =====================================================
    // ACT
    // =====================================================

    void Act()
    {
        foreach (var robot in warehouse.Robots)
        {
            MonitorBlocking(robot);
        }
    }

    // =====================================================
    // TASK MANAGEMENT
    // =====================================================

    void EnsureRobotHasTask(RobotAgent robot)
    {
        if (robot.HasPath)
            return;

        if (!robot.IsCarrying && unassignedCrates.Count == 0)
        {
            SendToParking(robot);
            return;
        }

        if (!robot.IsCarrying)
        {
            AssignPickup(robot);

            if (!robot.HasPath)
                SendToParking(robot);
        }
        else
        {
            AssignDrop(robot);
        }
    }

    void AssignPickup(RobotAgent robot)
    {
        if (unassignedCrates.Count == 0)
            return;

        var crate = unassignedCrates
            .OrderBy(c => Manhattan(robot.CurrentCell, c.CurrentCell))
            .FirstOrDefault();

        if (crate == null)
            return;

        var path = ComputePath(robot.CurrentCell, crate.CurrentCell);
        if (path.Count == 0)
            return;

        crateAssignments[crate] = robot;
        unassignedCrates.Remove(crate);

        parkedRobots.Remove(robot);
        robot.SetPath(path, crate.CurrentCell);
    }

    void AssignDrop(RobotAgent robot)
    {
        var target = warehouse.Grid
            .Cast<Cell>()
            .Where(c => c.IsDropZone && c.CanStack())
            .OrderBy(c => Manhattan(robot.CurrentCell, c))
            .FirstOrDefault();

        if (target == null)
            return;

        var path = ComputePath(robot.CurrentCell, target);
        if (path.Count == 0)
            return;

        robot.SetPath(path, target);
    }

    // =====================================================
    // PARKING SYSTEM (OWNERSHIP)
    // =====================================================

    void SendToParking(RobotAgent robot)
    {
        if (!parkingAssignments.TryGetValue(robot, out var parkingCell))
        {
            parkingCell = FindParkingCell();
            if (parkingCell == null)
                return;

            parkingAssignments[robot] = parkingCell;
        }

        if (robot.CurrentCell == parkingCell)
        {
            parkedRobots.Add(robot);
            return;
        }

        var path = ComputePath(robot.CurrentCell, parkingCell);
        if (path.Count == 0)
            return;

        parkedRobots.Add(robot);
        robot.SetPath(path, parkingCell);
    }

    Cell FindParkingCell()
    {
        for (int r = 1; r < warehouse.Rows / 4; r++)
        for (int c = 1; c < warehouse.Cols / 4; c++)
        {
            var cell = warehouse.Grid[r, c];

            if (!cell.IsShelf &&
                !cell.IsDropZone &&
                cell.OccupyingRobot == null &&
                !parkingAssignments.Values.Contains(cell))
                return cell;
        }

        return null;
    }

    // =====================================================
    // BLOCKING & REPLAN
    // =====================================================

    void MonitorBlocking(RobotAgent robot)
    {
        if (!robot.HasPath)
            return;

        var next = robot.NextCell;

        bool blocked =
            next.IsShelf ||
            (next.OccupyingRobot != null &&
             !parkedRobots.Contains(next.OccupyingRobot));

        if (!blocked)
        {
            blockedTicks[robot] = 0;
            return;
        }

        blockedTicks[robot]++;

        if (blockedTicks[robot] >= REPLAN_THRESHOLD)
        {
            blockedTicks[robot] = 0;
            Replan(robot);
        }
    }

    void Replan(RobotAgent robot)
    {
        if (robot.Goal == null)
        {
            robot.ClearPath();
            return;
        }

        var newPath = ComputePath(robot.CurrentCell, robot.Goal);

        if (newPath.Count > 0)
            robot.SetPath(newPath, robot.Goal);
        else
            robot.ClearPath();
    }

    // =====================================================
    // PATHFINDING
    // =====================================================

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
                if (next.IsShelf)
                    continue;

                if (next.OccupyingRobot != null && next != goal)
                    continue;

                if (cameFrom.ContainsKey(next))
                    continue;

                frontier.Enqueue(next);
                cameFrom[next] = current;
            }
        }

        if (!cameFrom.ContainsKey(goal))
            return new Queue<Cell>();

        var stack = new Stack<Cell>();
        var step = goal;

        while (step != start)
        {
            stack.Push(step);
            step = cameFrom[step];
        }

        return new Queue<Cell>(stack);
    }

    int Manhattan(Cell a, Cell b)
    {
        return System.Math.Abs(a.Row - b.Row) +
               System.Math.Abs(a.Col - b.Col);
    }
}