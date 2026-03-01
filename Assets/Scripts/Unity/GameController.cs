using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameController : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] int k = 20;
    [SerializeField] int robotCount = 5;

    [Header("Timing")]
    [SerializeField] float stepInterval = 0.3f;

    [Header("Prefabs")]
    public GameObject robotPrefab;
    public GameObject cratePrefab;
    public GameObject floorPrefab;

    Warehouse warehouse;
    WarehouseManager manager;

    Dictionary<RobotAgent, GameObject> robotViews = new();
    Dictionary<Crate, GameObject> crateViews = new();

    // tracks visual stacks at drop cells
    Dictionary<Cell, List<GameObject>> stackViews = new();

    float timer;

    // =========================
    // START
    // =========================

    void Start()
    {
        int size = Mathf.CeilToInt(Mathf.Sqrt(k * 10f));

        warehouse = new Warehouse(size, size);
        warehouse.Initialize(robotCount, k);

        manager = new WarehouseManager(warehouse);

        SpawnFloor(size);
        SpawnVisuals();
    }

    // =========================
    // SIMULATION LOOP
    // =========================

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= stepInterval)
        {
            timer = 0f;

            warehouse.StepSimulation();

            SyncRobotVisuals();
            SyncCrateVisuals();
            SyncStacks();
        }
    }

    // =========================
    // ROBOT VISUAL SYNC
    // =========================

    void SyncRobotVisuals()
    {
        foreach (var pair in robotViews)
        {
            var robot = pair.Key;
            var go = pair.Value;

            go.transform.position = CellToWorld(robot.CurrentCell);
        }
    }

    // =========================
    // CRATE PICKUP / REMOVAL
    // =========================

    void SyncCrateVisuals()
    {
        var toRemove = new List<Crate>();

        foreach (var pair in crateViews)
        {
            var crate = pair.Key;

            // crate picked by robot → remove visual
            if (crate.CurrentCell == null)
            {
                Destroy(pair.Value);
                toRemove.Add(crate);
            }
        }

        foreach (var c in toRemove)
            crateViews.Remove(c);
    }

    // =========================
    // STACK VISUALS AT DROP ZONES
    // =========================

    void SyncStacks()
    {
        foreach (var cell in warehouse.Grid)
        {
            if (!cell.IsDropZone || cell.StackHeight <= 0)
                continue;

            if (!stackViews.ContainsKey(cell))
                stackViews[cell] = new List<GameObject>();

            var stack = stackViews[cell];

            // spawn missing visual crates
            while (stack.Count < cell.StackHeight)
            {
                float height = 0.5f + stack.Count * 1.0f;

                Vector3 pos = CellToWorld(cell) + Vector3.up * height;

                var go = Instantiate(cratePrefab, pos, Quaternion.identity);
                stack.Add(go);
            }
        }
    }

    // =========================
    // SPAWN INITIAL VISUALS
    // =========================

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

    void SpawnFloor(int size)
    {
        var floor = Instantiate(floorPrefab);
        floor.transform.position = new Vector3(size / 2f, 0, size / 2f);
        floor.transform.localScale = new Vector3(size, 1, size);
    }

    Vector3 CellToWorld(Cell cell)
    {
        return new Vector3(cell.Row + 0.5f, 0, cell.Col + 0.5f);
    }
}