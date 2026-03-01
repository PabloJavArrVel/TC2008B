using System;
using System.Collections.Generic;

public class Warehouse
{
    public int Rows;
    public int Cols;

    public Cell[,] Grid;
    public List<RobotAgent> Robots = new();
    public List<Crate> Crates = new();

    Random random = new();

    public Warehouse(int rows, int cols)
    {
        Rows = rows;
        Cols = cols;

        Grid = new Cell[rows, cols];

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
            Grid[r, c] = new Cell { Row = r, Col = c };
    }

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
}