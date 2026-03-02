/*
========================================================
ROBOT AGENT — EXECUTOR ONLY (NO INTELLIGENCE)

RESPONSIBILITY:
- Store current position
- Store path assigned by manager
- Request next move
- Execute movement
- Pickup / drop when entering cell

FLOW:
WarehouseManager → SetPath()
Warehouse → RequestMove()
Warehouse → CommitMove()
========================================================
*/

using System;
using System.Collections.Generic;

public class RobotAgent
{
    public int Id { get; }

    public Cell CurrentCell { get; private set; }

    public Crate CarriedCrate { get; private set; }
    public bool IsCarrying => CarriedCrate != null;
    public event Action OnMoved;

    Queue<Cell> path = new();

    public bool HasPath => path.Count > 0;
    public Cell NextCell => path.Count > 0 ? path.Peek() : null;

    public Cell Goal { get; private set; }

    public RobotAgent(int id, Cell startCell)
    {
        Id = id;
        CurrentCell = startCell;
        startCell.OccupyingRobot = this;
    }

    // ================================
    // CALLED BY WAREHOUSE MANAGER
    // ================================

    public void SetPath(Queue<Cell> newPath, Cell goal)
    {
        path = newPath ?? new Queue<Cell>();
        Goal = goal;
    }
    public void ClearPath()
    {
        path.Clear();
        Goal = null;
    }
    // ================================
    // CALLED BY WAREHOUSE (EXECUTION)
    // ================================

    public MoveIntent RequestMove()
    {
        if (!HasPath)
            return Stay();

        return new MoveIntent
        {
            Robot = this,
            From = CurrentCell,
            To = NextCell
        };
    }

    MoveIntent Stay()
    {
        return new MoveIntent
        {
            Robot = this,
            From = CurrentCell,
            To = null
        };
    }

    public void CommitMove(Cell newCell)
    {
        CurrentCell = newCell;
        OnMoved?.Invoke();

        if (path.Count > 0)
            path.Dequeue();

        // PICKUP
        if (!IsCarrying && newCell.OccupyingCrate != null)
        {
            CarriedCrate = newCell.OccupyingCrate;
            newCell.OccupyingCrate = null;
            CarriedCrate.CurrentCell = null;
            ClearPath();
        }

        // DROP
        if (IsCarrying && newCell.CanStack())
        {
            newCell.StackHeight++;

            var deliveredCrate = CarriedCrate;
            CarriedCrate = null;

            // Remove crate from world entirely
            deliveredCrate.CurrentCell = null;
            deliveredCrate.IsDelivered = true; 

            ClearPath();
        }
    }
}