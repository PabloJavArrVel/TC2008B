# Warehouse Robot Simulation



## Overview

This project simulates a warehouse environment where multiple robots navigate a grid to collect crates and stack them at designated drop zones. The system models coordination, pathfinding, and conflict resolution between autonomous agents operating in a shared space.

The simulation is implemented in **Unity** using a grid-based world representation with discrete time steps.

---

# Unity Version

This project was developed using:

**Unity LTS 6000.3.9f1**

Using the same version is recommended to avoid package or serialization incompatibilities.

---

# Project Structure

The project follows a structured Unity asset organization to separate logic, assets, and scenes.

```
Assets
│
├── Art
│   ├── Materials
│   ├── Models
│   └── Textures
│
├── Prefabs
│   ├── Robots
│   ├── Crates
│   ├── Shelves
│   ├── Walls
│   └── Doors
│
├── Scenes
│   └── MainScene.unity
│
├── Scripts
│   ├── GameController.cs
│   ├── Warehouse.cs
│   ├── WarehouseManager.cs
│   ├── RobotAgent.cs
│   ├── Cell.cs
│   └── Crate.cs
│
└── UI
```

### Key Folders

**Art/**
Contains raw art assets used in the project.

* **Materials** – Unity materials applied to models.
* **Models** – Imported 3D models (.fbx).
* **Textures** – Surface textures used by materials.

**Prefabs/**
Reusable objects instantiated during simulation, such as robots, crates, shelves, and environment elements.

**Scenes/**
Unity scenes containing the playable simulation environment.

**Scripts/**
Core logic of the simulation, including:

* Warehouse world state
* Robot behavior
* Task allocation
* Pathfinding
* Simulation loop

---

# Core Architecture

The simulation follows a **centralized management architecture**.

### Warehouse

Responsible for the **world state and physics rules**.

Handles:

* Grid structure
* Movement validation
* Conflict resolution
* Atomic movement updates

### WarehouseManager

Acts as the **decision-making layer**.

Handles:

* Task allocation
* Path computation
* Blocking detection
* Replanning strategies

### RobotAgent

Executes instructions provided by the manager.

Responsibilities:

* Store current path
* Request moves
* Pick up crates
* Drop crates

### GameController

Unity entry point responsible for:

* Scene initialization
* Spawning visuals
* Synchronizing simulation with visuals
* Managing UI and timing

---

# Simulation Flow

Each simulation step follows a **Perceive → Deliberate → Act loop**:

1. **Perceive**

   * Manager observes world state.
   * Updates crate assignments.

2. **Deliberate**

   * Assigns pickup or drop tasks to robots.
   * Sends idle robots to parking areas.

3. **Act**

   * Robots request moves.
   * Warehouse validates moves.
   * Conflicts are resolved.
   * Moves are applied atomically.

---

# Environment Generation

The warehouse grid size is computed dynamically based on the number of robots and crates to avoid overcrowding.

The environment contains:

* **Shelves** (obstacles)
* **Drop zones** for stacking crates
* **Parking areas** for idle robots
* **Perimeter walls**

Drop zones are placed in the **center of warehouse quadrants** to reduce congestion.

---

# Asset Credits

The following assets were used in this project:

**Door**
Kay Lousberg
https://poly.pizza/m/MSIuI2jpqb

**Corrugated Iron Sheet**
Kenney
https://poly.pizza/m/jzqMyr29tn

**Rover (Robot)**
Quaternius
https://poly.pizza/m/tzOLXetacM

**Scifi Crate**
Quaternius
https://poly.pizza/m/bPeXlVjwCH

**Shelves**
J-Toastie
License: CC-BY 3.0
https://creativecommons.org/licenses/by/3.0/
https://poly.pizza/m/OD78iJOQoN

**Concrete Texture**
Rob Tuytel
https://polyhaven.com/a/concrete_layers_02

---

# Running the Simulation

1. Open the project in **Unity 6000.4.0f1**
2. Load the scene:

```
Assets/Scenes/WarehouseScene
```

3. Press **Play**

Simulation parameters can be adjusted in the **GameController inspector**:

* Number of robots
* Number of crates
* Simulation speed
* Maximum simulation time

---

# Future Improvements

Possible future extensions include:

* Advanced pathfinding (A*)
* Dynamic obstacle avoidance
* Improved traffic management
* Adaptive drop zone placement based on crate distribution
* Performance optimizations for large-scale simulations

---

# License

This project is intended for educational purposes.
Third-party assets remain under their respective licenses.
