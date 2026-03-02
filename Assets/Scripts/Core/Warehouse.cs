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
    Random random = new();

    public WarehouseManager Manager { get; set; }

    // =========================
    // CONSTRUCTOR
    // =========================

    public Warehouse(int rows, int cols)
    {
        Rows = rows;
        Cols = cols;

        Grid = new Cell[rows, cols];

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            Grid[r, c] = new Cell { Row = r, Col = c };
    }

    // =========================
    // INITIALIZATION
    // =========================

    public void Initialize(int robotCount, int crateCount)
    {
        SpawnCrates(crateCount);
        SpawnRobots(robotCount);
    }

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

    // =========================
    // SIMULATION STEP
    // =========================

    public void StepSimulation()
    {
        Manager?.Tick();
        // 1 — collect intents from robots
        var intents = new List<MoveIntent>();

        foreach (var robot in Robots)
            intents.Add(robot.Step());

        // 2 — resolve conflicts
        ResolveConflicts(intents);

        // 3 — apply approved moves
        ApplyMoves(intents);
    }

    // =========================
    // CONFLICT RESOLUTION
    // =========================

    void ResolveConflicts(List<MoveIntent> intents)
    {
        // --- multiple robots wanting same cell ---
        var targetGroups = intents
            .Where(i => i.To != null)
            .GroupBy(i => i.To);

        foreach (var group in targetGroups)
        {
            if (group.Count() <= 1)
                continue;

            // lowest ID wins
            var winner = group.OrderBy(i => i.Robot.Id).First();

            foreach (var loser in group)
            {
                if (loser != winner)
                    loser.To = null;
            }
        }

        // --- prevent swaps (A→B and B→A) ---
        for (int i = 0; i < intents.Count; i++)
        for (int j = i + 1; j < intents.Count; j++)
        {
            var a = intents[i];
            var b = intents[j];

            if (a.To == null || b.To == null)
                continue;

           if (a.To == b.From && b.To == a.From)
            {
                if (a.Robot.Id < b.Robot.Id)
                    b.To = null;
                else
                    a.To = null;
            }
        }
    }

    // =========================
    // APPLY MOVES (ATOMIC)
    // =========================

    void ApplyMoves(List<MoveIntent> intents)
    {
        foreach (var intent in intents)
        {
            if (intent.To == null)
                continue;

            // update world
            intent.From.OccupyingRobot = null;
            intent.To.OccupyingRobot = intent.Robot;

            // notify robot
            intent.Robot.CommitMove(intent.To);
        }
    }

    // =========================
    // OPTIONAL HELPERS
    // =========================

    public IEnumerable<Cell> GetNeighbors(Cell cell)
    {
        int r = cell.Row;
        int c = cell.Col;

        if (r > 0) yield return Grid[r - 1, c];
        if (r < Rows - 1) yield return Grid[r + 1, c];
        if (c > 0) yield return Grid[r, c - 1];
        if (c < Cols - 1) yield return Grid[r, c + 1];
    }
}