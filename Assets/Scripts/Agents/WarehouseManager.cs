using System;
using System.Collections.Generic;
using System.Linq;

public class WarehouseManager
{
    private Warehouse warehouse;

    private List<Crate> unassignedCrates = new();
    private Queue<Cell> dropZones = new();
    private Dictionary<RobotAgent, int> stuckCounter = new();

    // Rastrea qué caja fue asignada a cada robot (antes de recogerla)
    // Para devolver cajas huérfanas si el robot replannea
    private Dictionary<RobotAgent, Crate> assignedCrates = new();


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
            stuckCounter[robot] = 0;

        foreach (var robot in warehouse.Robots)
            AssignNextTask(robot);
    }

    // =========================
    // INITIALIZE CRATES
    // =========================

    void InitializeCrateQueue()
    {
        foreach (var crate in warehouse.Crates)
            unassignedCrates.Add(crate);
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
        // limpiar crates ya recogidas
        unassignedCrates.RemoveAll(c => c.CurrentCell == null);

        if (unassignedCrates.Count == 0)
        {
            // Si está parado sobre una drop zone, moverse YA para no bloquearla
            if (robot.CurrentCell.IsDropZone)
                MoveToParking(robot);
            return;
        }

        // asignar la más cercana que tenga path válido
        var sorted = unassignedCrates
            .OrderBy(c => Math.Abs(robot.CurrentCell.Row - c.CurrentCell.Row)
                        + Math.Abs(robot.CurrentCell.Col - c.CurrentCell.Col));

        foreach (var crate in sorted)
        {
            var path = ComputePath(robot.CurrentCell, crate.CurrentCell);
            if (path.Count > 0)
            {
                unassignedCrates.Remove(crate);
                assignedCrates[robot] = crate; // rastrear asignación
                robot.AssignPath(path);
                return;
            }
        }

        // Ninguna caja alcanzable ahora: esperar idle, Tick() reintentará
    }

    void MoveToParking(RobotAgent robot)
    {
        var frontier = new Queue<Cell>();
        var visited = new HashSet<Cell>();

        frontier.Enqueue(robot.CurrentCell);
        visited.Add(robot.CurrentCell);

        Cell parkingSpot = null;

        while (frontier.Count > 0)
        {
            var curr = frontier.Dequeue();

            if (curr.IsFree() && curr != robot.CurrentCell)
            {
                parkingSpot = curr;
                break;
            }

            foreach (var next in warehouse.GetNeighbors(curr))
            {
                if (!visited.Contains(next) && !next.IsShelf)
                {
                    visited.Add(next);
                    frontier.Enqueue(next);
                }
            }
        }

        if (parkingSpot != null)
        {
            var path = ComputePath(robot.CurrentCell, parkingSpot);
            robot.AssignPath(path);
        }
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
            if (path.Count > 0)
            {
                robot.AssignPath(path);
                return;
            }

        }

        // Sin drop zone disponible: el robot espera idle.
        // Tick() reintentará sin causar oscilación.
    }

    // =====================================================
    // DROP ZONE INITIALIZATION
    // =====================================================

    void InitializeDropZones(int crateCount)
    {
        dropZones.Clear();

        // Ceiling division: siempre hay suficiente capacidad para todas las cajas
        int stackCount = (crateCount + 4) / 5;
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

                // No poner drop zone sobre estante ni sobre una caja suelta
                if (!cell.IsShelf && cell.OccupyingCrate == null && !result.Contains(cell))
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
                if ((next.IsShelf || next.HasLooseCrate()) && next != goal)
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

    public void Tick()
    {
        // Limpiar asignaciones de robots que ya recogieron su caja
        foreach (var robot in warehouse.Robots)
            if (robot.IsCarrying && assignedCrates.ContainsKey(robot))
                assignedCrates.Remove(robot);

        foreach (var robot in warehouse.Robots)
        {
            if (robot.State == RobotState.Idle)
            {
                // Robot idle parado sobre drop zone: moverse para no bloquearla
                if (robot.CurrentCell.IsDropZone)
                {
                    MoveToParking(robot);
                    stuckCounter[robot] = 0;
                    continue;
                }

                stuckCounter[robot]++;

                // Reintentar asignación cada 8 ticks para no oscilar
                if (stuckCounter[robot] >= 8)
                {
                    stuckCounter[robot] = 0;
                    AssignNextTask(robot);
                }
                continue;
            }

            if (robot.State != RobotState.Tasked) continue;

            if (robot.IsBlocked)
            {
                stuckCounter[robot]++;

                if (stuckCounter[robot] >= 8)
                {
                    stuckCounter[robot] = 0;

                    // Si el bloqueo es un robot idle, pedirle que ceda el paso
                    var blocker = robot.NextIntendedCell?.OccupyingRobot;
                    if (blocker != null && blocker.State == RobotState.Idle)
                    {
                        MoveToParking(blocker);
                        stuckCounter[blocker] = 0;
                    }
                    else
                    {
                        // Devolver caja huérfana a la cola antes de replanear
                        if (assignedCrates.TryGetValue(robot, out var orphan)
                            && orphan.CurrentCell != null
                            && !unassignedCrates.Contains(orphan))
                        {
                            unassignedCrates.Add(orphan);
                        }
                        assignedCrates.Remove(robot);

                        robot.ClearPath();
                        AssignNextTask(robot);
                    }
                }
            }
            else
            {
                stuckCounter[robot] = 0;
            }
        }
    }
}
