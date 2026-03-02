using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class GameController : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] int k = 20;
    [SerializeField] int robotCount = 5;

    [Header("Timing")]
    [SerializeField] float stepInterval = 0.3f;
    [SerializeField] float maxTime = 300f;

    [Header("Prefabs")]
    public GameObject robotPrefab;
    public GameObject cratePrefab;
    public GameObject floorPrefab;
    public GameObject wallStraightPrefab;
    public GameObject wallCornerPrefab;
    public GameObject shelfPrefab;

    [Header("UI")]
    public Text timeText;
    public Text movesText;
    public Text statusText;

    Warehouse warehouse;
    WarehouseManager manager;

    Dictionary<RobotAgent, GameObject> robotViews = new();
    Dictionary<Crate, GameObject> crateViews = new();
    Dictionary<Cell, List<GameObject>> stackViews = new();

    float timer;
    float elapsed;
    int totalMoves;
    bool simDone;

    int rows, cols;

    // =========================
    // START
    // =========================

    void Start()
    {
        // almacén más grande para acomodar estantes y pasillos
        rows = 20;
        cols = 20;

        warehouse = new Warehouse(rows, cols);

        MarkShelves();

        warehouse.Initialize(robotCount, k);

        manager = new WarehouseManager(warehouse);
        warehouse.Manager = manager;

        foreach (var robot in warehouse.Robots)
            robot.OnMoved += () => totalMoves++;

        SpawnFloor();
        SpawnPerimeterWalls(5f);
        SpawnShelves();
        SpawnVisuals();
    }

    // =========================
    // MARCAR CELDAS DE ESTANTES
    // =========================

    void MarkShelves()
    {
        int[] shelfRowIndices = { 3, 6, 9, 12, 15 };

        foreach (int r in shelfRowIndices)
        {
            if (r >= rows) continue;

            // Empezamos en 2 y terminamos en cols-3 para dejar pasillos a los lados
            for (int c = 2; c <= cols - 3; c++)
            {
                // Dejamos un pasillo central extra para que fluyan mejor
                if (c == cols / 2 || c == (cols / 2) - 1) 
                    continue; 

                warehouse.Grid[r, c].IsShelf = true;
            }
        }

        // Marcar bordes como no transitables (paredes)
        for (int c = 0; c < cols; c++)
        {
            warehouse.Grid[0, c].IsShelf = true;
            warehouse.Grid[rows - 1, c].IsShelf = true;
        }
        for (int r = 0; r < rows; r++)
        {
            warehouse.Grid[r, 0].IsShelf = true;
            warehouse.Grid[r, cols - 1].IsShelf = true;
        }
    }

    // =========================
    // SIMULATION LOOP
    // =========================

    void Update()
    {
        if (simDone) return;

        elapsed += Time.deltaTime;
        timer += Time.deltaTime;

        if (elapsed >= maxTime)
        {
            simDone = true;
            if (statusText) statusText.text = "⏱ Tiempo agotado";
            return;
        }

        if (timer >= stepInterval)
        {
            timer = 0f;
            warehouse.StepSimulation();
            SyncRobotVisuals();
            SyncCrateVisuals();
            SyncStacks();
            CheckDone();
        }

        UpdateUI();
    }

    // =========================
    // CHECK DONE
    // =========================

    void CheckDone()
    {
        bool allStacked = warehouse.Crates.All(c => c.CurrentCell == null);
        bool noneCarrying = warehouse.Robots.All(r => !r.IsCarrying);

        if (allStacked && noneCarrying)
        {
            simDone = true;
            if (statusText) statusText.text = "✅ ¡Todas las cajas apiladas!";
        }
    }

    // =========================
    // UI
    // =========================

    void UpdateUI()
    {
        if (timeText)
            timeText.text = $"Tiempo: {elapsed:F1}s";

        if (movesText)
            movesText.text = $"Movimientos: {totalMoves}";

        if (statusText && !simDone)
            statusText.text = $"Cajas restantes: {warehouse.Crates.Count(c => c.CurrentCell != null)}";
    }

    // =========================
    // ROBOT VISUAL SYNC
    // =========================

    void SyncRobotVisuals()
    {
        foreach (var pair in robotViews)
            pair.Value.transform.position = CellToWorld(pair.Key.CurrentCell);
    }

    // =========================
    // CRATE PICKUP / REMOVAL
    // =========================

    void SyncCrateVisuals()
    {
        var toRemove = new List<Crate>();

        foreach (var pair in crateViews)
        {
            if (pair.Key.CurrentCell == null)
            {
                Destroy(pair.Value);
                toRemove.Add(pair.Key);
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

            while (stack.Count < cell.StackHeight)
            {
                float height = 0.5f + stack.Count * 1.0f;
                var go = Instantiate(cratePrefab,
                    CellToWorld(cell) + Vector3.up * height,
                    Quaternion.identity);
                stack.Add(go);
            }
        }
    }

    // =========================
    // SPAWN VISUALS
    // =========================

    void SpawnVisuals()
    {
        foreach (var crate in warehouse.Crates)
            crateViews[crate] = Instantiate(cratePrefab, CellToWorld(crate.CurrentCell), Quaternion.identity);

        foreach (var robot in warehouse.Robots)
            robotViews[robot] = Instantiate(robotPrefab, CellToWorld(robot.CurrentCell), Quaternion.identity);
    }

    // =========================
    // SPAWN FLOOR
    // =========================

    void SpawnFloor()
    {
        var floor = Instantiate(floorPrefab);
        floor.transform.position = new Vector3(cols / 2f, 0f, rows / 2f);
        floor.transform.localScale = new Vector3(cols, 0.1f, rows);
        
        var ceiling = Instantiate(floorPrefab);
        ceiling.transform.position = new Vector3(cols / 2f, 10f, rows / 2f);
        ceiling.transform.localScale = new Vector3(cols, 0.1f, rows);
    }

   void SpawnPerimeterWalls(float y)
    {
        float offset = 90f;
        
        for (int c = 0; c < cols; c++)
        {
            SpawnWall(wallStraightPrefab, c + .5f, rows - 1, y, 0f + offset);   // Norte
            SpawnWall(wallStraightPrefab, c -.5f, 0, y, 180f + offset);
        }
        for (int r = 1; r < rows; r++)
        {
            SpawnWall(wallStraightPrefab, cols -.5f, r - 1, y, 90f + offset);  // Este
            SpawnWall(wallStraightPrefab, -.5f, r, y, 270f + offset);        // Oeste
        }
    }

    void SpawnWall(GameObject prefab, float col, float row, float y, float rotY)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab);
        go.transform.position = new Vector3(col + 0.5f, y, row + 0.5f);
        go.transform.rotation = Quaternion.Euler(0, rotY, 0);

        go.transform.localScale = new Vector3(1f, 10f, 1f);
    }

    // =========================
    // SPAWN SHELVES
    // =========================

    void SpawnShelves()
    {
        if (shelfPrefab == null) return;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (warehouse.Grid[r, c].IsShelf)
            {
                // no spawnear en celdas de borde (esas son paredes)
                if (r == 0 || r == rows - 1 || c == 0 || c == cols - 1)
                    continue;

                var go = Instantiate(shelfPrefab);
                go.transform.position = new Vector3(c + 0.5f, 0f, r + 0.5f);
            }
        }
    }

    // =========================
    // HELPER
    // =========================

    Vector3 CellToWorld(Cell cell)
    {
        return new Vector3(cell.Col + 0.5f, 0.5f, cell.Row + 0.5f);
    }
}