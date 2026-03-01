using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    public int rows = 20;
    public int cols = 20;

    public int robotCount = 5;
    public int crateCount = 20;

    public GameObject robotPrefab;
    public GameObject cratePrefab;
    public GameObject floorPrefab;

    Warehouse warehouse;

    Dictionary<RobotAgent, GameObject> robotViews = new();
    Dictionary<Crate, GameObject> crateViews = new();

    void Start()
    {
        warehouse = new Warehouse(rows, cols);
        warehouse.Initialize(robotCount, crateCount);

        GenerateVisualGrid();
        GenerateFloor();
        SpawnVisuals();
    }

    void GenerateFloor()
    {
        GameObject floor = Instantiate(floorPrefab);

        floor.transform.position = new Vector3(rows / 2f, 0, cols / 2f);

        floor.transform.localScale = new Vector3(rows, 1, cols);
    }

    void GenerateVisualGrid()
    {
        for (int r = 0; r <= rows; r++)
        {
            CreateLine(
                new Vector3(0, 0.01f, r),
                new Vector3(cols, 0.01f, r)
            );
        }

        for (int c = 0; c <= cols; c++)
        {
            CreateLine(
                new Vector3(c, 0.01f, 0),
                new Vector3(c, 0.01f, rows)
            );
        }
    }

    void CreateLine(Vector3 start, Vector3 end)
    {
        GameObject line = new GameObject("GridLine");
        LineRenderer lr = line.AddComponent<LineRenderer>();

        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.widthMultiplier = 0.05f;
    }

    void SpawnVisuals()
    {
        foreach (var crate in warehouse.Crates)
        {
            Vector3 pos = CellToWorld(crate.CurrentCell);
            crateViews[crate] = Instantiate(cratePrefab, pos, Quaternion.identity);
        }

        foreach (var robot in warehouse.Robots)
        {
            Vector3 pos = CellToWorld(robot.CurrentCell);
            robotViews[robot] = Instantiate(robotPrefab, pos, Quaternion.identity);
        }
    }

    Vector3 CellToWorld(Cell cell)
    {
        return new Vector3(cell.Row + 0.5f, 0, cell.Col + 0.5f);
    }
}