/*
========================================================
WAREHOUSE — WORLD + PHYSICS ENGINE

RESPONSIBILITY:
- Own the world state (grid, robots, crates, walls)
- Enforce physics rules
- Validate movement legality
- Resolve movement conflicts
- Apply moves atomically
- Execute simulation step

StepSimulation()
    1. Manager thinks (assigns paths / replans)
    2. Robots request moves
    3. Warehouse validates moves
    4. Warehouse resolves conflicts
    5. Warehouse applies moves atomically
========================================================
*/

using System;
using System.Collections.Generic;
using System.Linq;

public class Warehouse
{
    public int Rows { get; }
    public int Cols { get; }

    public Cell[,] Grid { get; }
    public List<RobotAgent> Robots { get; } = new();
    public List<Crate> Crates { get; } = new();

    public WarehouseManager Manager { get; set; }

    int conflictRound = 0; 
    System.Random conflictRandom = new System.Random(); 


    Random random = new();

    // =====================================================
    // CONSTRUCTOR
    // =====================================================

    public Warehouse(int rows, int cols)
    {
        Rows = rows;
        Cols = cols;

        Grid = new Cell[rows, cols];

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            Grid[r, c] = new Cell { Row = r, Col = c };
    }

    // =====================================================
    // WORLD GENERATION 
    // =====================================================

    public void GenerateWorld(int robotCount, int crateCount)
    {
        MarkShelves();
        GenerateQuadrantDropZones(crateCount);
        SpawnCrates(crateCount);
        SpawnRobots(robotCount);
    }

    // =====================================================
    // SHELVES 
    // =====================================================

    void MarkShelves()
    {
        int[] shelfRowIndices = { 3, 6, 9, 12, 15 };

        foreach (int r in shelfRowIndices)
        {
            if (r >= Rows) continue;

            for (int c = 2; c <= Cols - 3; c++)
            {
                if (c == Cols / 2 || c == (Cols / 2) - 1)
                    continue;

                Grid[r, c].IsShelf = true;
            }
        }

        // Perimeter logical walls
        for (int c = 0; c < Cols; c++)
        {
            Grid[0, c].IsShelf = true;
            Grid[Rows - 1, c].IsShelf = true;
        }

        for (int r = 0; r < Rows; r++)
        {
            Grid[r, 0].IsShelf = true;
            Grid[r, Cols - 1].IsShelf = true;
        }
    }

    // =====================================================
    // DROP ZONES (radial around quadrants center)
    // =====================================================

    void GenerateQuadrantDropZones(int crateCount)
    {
        int stacksNeeded = (int)Math.Ceiling(crateCount / 5.0);

        int midRow = Rows / 2;
        int midCol = Cols / 2;

        // Define quadrant centers
        var quadrantCenters = new List<(int r, int c)>
        {
            (midRow / 2,         midCol / 2),          // Top-Left
            (midRow / 2,         midCol + midCol / 2), // Top-Right
            (midRow + midRow/2,  midCol / 2),          // Bottom-Left
            (midRow + midRow/2,  midCol + midCol/2)    // Bottom-Right
        };

        int stacksPerQuadrant = stacksNeeded / 4;
        int remainder = stacksNeeded % 4;

        for (int q = 0; q < 4; q++)
        {
            int stacksHere = stacksPerQuadrant + (q < remainder ? 1 : 0);

            if (stacksHere == 0)
                continue;

            var center = quadrantCenters[q];

            PlaceRadialStacksFromCenter(center.r, center.c, stacksHere);
        }
    }

    void PlaceRadialStacksFromCenter(int centerR, int centerC, int count)
    {
        int placed = 0;
        int radius = 0;

        while (placed < count)
        {
            for (int dr = -radius; dr <= radius && placed < count; dr++)
            {
                for (int dc = -radius; dc <= radius && placed < count; dc++)
                {
                    if (Math.Abs(dr) != radius && Math.Abs(dc) != radius)
                        continue; // only ring perimeter

                    int r = centerR + dr;
                    int c = centerC + dc;

                    if (!IsInside(r, c))
                        continue;

                    var cell = Grid[r, c];

                    if (cell.IsShelf || cell.IsDropZone)
                        continue;

                    cell.IsDropZone = true;
                    placed++;
                }
            }

            radius++;
        }
    }

    bool IsInside(int r, int c)
    {
        return r > 0 && r < Rows - 1 &&
               c > 0 && c < Cols - 1;
    }

    // =====================================================
    // SPAWN ENTITIES
    // =====================================================

    void SpawnCrates(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Cell cell = GetRandomFreeCell();
            Crates.Add(new Crate(i, cell));
        }
    }

    void SpawnRobots(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Cell cell = GetRandomFreeCell();
            Robots.Add(new RobotAgent(i, cell));
        }
    }

    Cell GetRandomFreeCell()
    {
        while (true)
        {
            int r = random.Next(Rows);
            int c = random.Next(Cols);

            if (Grid[r, c].IsFree())
                return Grid[r, c];
        }
    }

    // =====================================================
    // SIMULATION STEP 
    // =====================================================

    public void StepSimulation()
    {
        Manager?.Tick();

        var intents = CollectMoveIntents();
        ValidateMoves(intents);
        ResolveConflicts(intents);
        ApplyMoves(intents);
    }

    List<MoveIntent> CollectMoveIntents()
    {
        var intents = new List<MoveIntent>();
        foreach (var robot in Robots)
            intents.Add(robot.RequestMove());
        return intents;
    }

    void ValidateMoves(List<MoveIntent> intents)
    {
        var intentByRobot = intents.ToDictionary(i => i.Robot, i => i);

        foreach (var intent in intents)
        {
            if (intent.To == null)
                continue;

            if (intent.To.IsShelf)
            {
                intent.To = null;
                continue;
            }

            var occupant = intent.To.OccupyingRobot;

            if (occupant != null)
            {
                if (!intentByRobot.TryGetValue(occupant, out var occupantIntent))
                {
                    intent.To = null;
                    continue;
                }

                // occupant staying
                if (occupantIntent.To == null)
                {
                    intent.To = null;
                    continue;
                }

                // prevent swaps
                if (occupantIntent.To == intent.From)
                {
                    intent.To = null;
                    continue;
                }
            }
        }
    }

    void ResolveConflicts(List<MoveIntent> intents)
    {
        // -------------------------------------------------
        // MULTIPLE ROBOTS WANT SAME CELL
        // Use randomized/rotating tie-break to avoid starvation.
        // -------------------------------------------------
        var targetGroups = intents
            .Where(i => i.To != null)
            .GroupBy(i => i.To);

        foreach (var group in targetGroups)
        {
            if (group.Count() <= 1)
                continue;

            // Produce a shuffled order so we don't always pick lowest id
            // Shuffle deterministic-ish using conflictRound and robot id to avoid complete nondeterminism.
            var ordered = group.OrderBy(i => (i.Robot.Id + conflictRound) % (group.Count() + 1)).ToList();

            // pick first as winner
            var winner = ordered.First();

            foreach (var loser in group)
                if (loser != winner)
                    loser.To = null;
        }

        // -------------------------------------------------
        // PREVENT SWAPS (A→B and B→A)
        // -------------------------------------------------
        for (int i = 0; i < intents.Count; i++)
        for (int j = i + 1; j < intents.Count; j++)
        {
            var a = intents[i];
            var b = intents[j];

            if (a.To == null || b.To == null)
                continue;

            if (a.To == b.From && b.To == a.From)
            {
                // break swap by id parity or randomized tie-break
                if ((a.Robot.Id + conflictRound) % 2 == 0)
                    b.To = null;
                else
                    a.To = null;
            }
        }

        // advance conflictRound each tick so tie-breakers rotate
        conflictRound++;
    }

   void ApplyMoves(List<MoveIntent> intents)
    {
        // Determine final positions first
        var finalPositions = new Dictionary<RobotAgent, Cell>();

        foreach (var intent in intents)
        {
            if (intent.To != null)
                finalPositions[intent.Robot] = intent.To;
            else
                finalPositions[intent.Robot] = intent.From;
        }

        // Clear all robot occupancy
        foreach (var robot in Robots)
            robot.CurrentCell.OccupyingRobot = null;

        // Apply final positions
        foreach (var kv in finalPositions)
        {
            var robot = kv.Key;
            var cell = kv.Value;

            cell.OccupyingRobot = robot;
            robot.CommitMove(cell);
        }

        Crates.RemoveAll(c => c.IsDelivered);
    }

    public IEnumerable<Cell> GetNeighbors(Cell cell)
    {
        int r = cell.Row;
        int c = cell.Col;

        if (r > 0) yield return Grid[r - 1, c];
        if (r < Rows - 1) yield return Grid[r + 1, c];
        if (c > 0) yield return Grid[r, c - 1];
        if (c < Cols - 1) yield return Grid[r, c + 1];
    }
    // returns nearest free cell (not shelf, not drop zone unless you want), or null if none
    public Cell FindNearestFreeCell(Cell start)
    {
        var frontier = new Queue<Cell>();
        var visited = new HashSet<Cell>();

        frontier.Enqueue(start);
        visited.Add(start);

        while (frontier.Count > 0)
        {
            var cur = frontier.Dequeue();

            // skip current occupying robot (we're looking for a free target)
            if (cur.IsFree())
                return cur;

            foreach (var n in GetNeighbors(cur))
            {
                if (visited.Contains(n)) continue;
                if (n.IsShelf) continue;
                visited.Add(n);
                frontier.Enqueue(n);
            }
        }

        return null;
    }
}