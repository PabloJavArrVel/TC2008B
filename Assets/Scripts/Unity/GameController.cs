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
    public GameObject doorPrefab;
    public GameObject shelfPrefab;

    [Header("UI")]
    public Light directionalLight;
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

    Dictionary<RobotAgent, Vector3> robotTargetPositions = new();

    // =========================
    // START
    // =========================

    void Start()
    {
        // --- Estimate required cells ---
        int requiredCells = k * 4 + robotCount * 2;

        size = Mathf.CeilToInt(size / (float)spacing) * spacing;

        rows = Mathf.Max(size, 20);
        cols = Mathf.Max(size, 20);

        // --- Create warehouse ---
        warehouse = new Warehouse(rows, cols);
        warehouse.GenerateWorld(robotCount, k);

        manager = new WarehouseManager(warehouse);
        warehouse.Manager = manager;

        foreach (var robot in warehouse.Robots)
            robot.OnMoved += () => totalMoves++;

        SpawnFloor();
        SpawnPerimeterWalls(5f);
        SpawnDoor(cols / 2f, rows - 1, 0f, 180f); // north wall door
        SpawnShelves();
        SpawnVisuals();
        PositionLights();
        PositionCamera();
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
        SmoothMoveRobots();
    }

    void SmoothMoveRobots()
{
    float speed = 1f / stepInterval;

    foreach (var pair in robotViews)
    {
        var target = robotTargetPositions[pair.Key];
        pair.Value.transform.position = Vector3.MoveTowards(
            pair.Value.transform.position,
            target,
            speed * Time.deltaTime
        );
    }
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
            robotTargetPositions[pair.Key] = CellToWorld(pair.Key.CurrentCell); 
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

        foreach (var robot in warehouse.Robots){
            var pos = CellToWorld(robot.CurrentCell);
            robotViews[robot] = Instantiate(robotPrefab, pos, Quaternion.identity);
            robotTargetPositions[robot] = pos;
        }
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

    // =========================
    // SPAWN WALLS
    // =========================

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
    // SPAWN DOOR
    // =========================
    void SpawnDoor(float col, float row, float y, float rotY)
    {
        if (doorPrefab == null) return;

        var door = Instantiate(doorPrefab);

        door.transform.position = new Vector3(col + 0.5f, y + 0.1f, row + 0.5f);
        door.transform.rotation = Quaternion.Euler(0, rotY, 0);
    }

    // =========================
    // SPAWN SHELVES
    // =========================

    void SpawnShelves()
    {
        if (shelfPrefab == null) return;

        for (int r = 0; r < warehouse.Rows; r++)
        for (int c = 0; c < warehouse.Cols; c++)
        {
            var cell = warehouse.Grid[r, c];

            // Only render actual shelf cells
            if (!cell.IsShelf)
                continue;

            // Skip perimeter (those are logical walls, rendered separately)
            if (r == 0 || r == warehouse.Rows - 1 ||
                c == 0 || c == warehouse.Cols - 1)
                continue;

            Instantiate(
                shelfPrefab,
                CellToWorld(cell),
                Quaternion.identity
            );
        }
    }

    // =========================
    // HELPER
    // =========================

    Vector3 CellToWorld(Cell cell)
    {
        return new Vector3(cell.Col + 0.5f, 0.5f, cell.Row + 0.5f);
    }

    void PositionCamera()
    {
        float gridSize = Mathf.Max(rows, cols);

        float centerX = cols / 2f;
        float centerZ = rows / 2f;

        Camera cam = Camera.main;

        cam.transform.rotation = Quaternion.Euler(51.88f, 0f, 0f);

        // Scale camera position with grid size
        float height = gridSize * 0.5f;
        float zOffset = gridSize * 0.455f;

        cam.transform.position = new Vector3(
            centerX,
            height,
            centerZ - zOffset
        );

    }

        // =====================
        // Directional Light
        // =====================
        
    void PositionLights()
    {
        float centerX = rows / 2f;
        float centerZ = cols / 2f;

        float margin = 1.5f;   // distance from edges
        float height = 5f;

        Vector3[] positions =
        {
            // corners (inside grid)
            new Vector3(margin, height, margin),
            new Vector3(rows - margin, height, margin),
            new Vector3(margin, height, cols - margin),
            new Vector3(rows - margin, height, cols - margin),

            // cross pattern
            new Vector3(centerX, height, margin),
            new Vector3(centerX, height, cols - margin),
            new Vector3(margin, height, centerZ),
            new Vector3(rows - margin, height, centerZ)
        };

        Vector3 center = new Vector3(centerX, 0, centerZ);

        foreach (var pos in positions)
        {
            var light = Instantiate(directionalLight);
            light.transform.position = pos;
            light.transform.rotation = Quaternion.LookRotation(center - pos);
        }
    }
}