using System;
using System.Collections.Generic;

public class RobotAgent
{
    public int Id { get; }
    public Cell CurrentCell { get; private set; }

    public RobotState State { get; private set; } = RobotState.Idle;

    // ======================
    // REAL CARRY SYSTEM
    // ======================

    public Crate CarriedCrate { get; private set; }
    public bool IsCarrying => CarriedCrate != null;

    private Queue<Cell> path = new();

    public event Action<RobotAgent> OnTaskFinished;
    public event Action OnMoved;
    public bool IsBlocked { get; private set; }
    public Cell NextIntendedCell => (State == RobotState.Tasked && path.Count > 0) ? path.Peek() : null;
    private Cell goalCell;

    // deadlock prevention (only for Tasked+Blocked state, not Idle)
    int blockedSteps = 0;

    public RobotAgent(int id, Cell startCell)
    {
        Id = id;
        CurrentCell = startCell;
        startCell.OccupyingRobot = this;
    }

    // =====================
    // MANAGER API
    // =====================

    public void AssignPath(Queue<Cell> newPath)
    {
        path = newPath ?? new Queue<Cell>();
        blockedSteps = 0;

        State = path.Count > 0
            ? RobotState.Tasked
            : RobotState.Idle;
    }

    // =====================
    // PERCEIVE → DECIDE
    // =====================

    public MoveIntent Step()
    {
        var p = Perceive();

        if (p.PathFinished)
        {
            if (State == RobotState.Tasked)
            {
                State = RobotState.Idle;
                OnTaskFinished?.Invoke(this);
            }
            return Stay();
        }

        if (State == RobotState.Idle || path.Count == 0)
            return Stay();

        if (p.NextIsBlocked)
        {
            IsBlocked = true; 
            return Stay();
        }

        IsBlocked = false;

        return new MoveIntent
        {
            Robot = this,
            From = CurrentCell,
            To = p.NextCell
        };
    }
    private Perception Perceive()
    {
        if (State != RobotState.Tasked || path.Count == 0)
            return new Perception { PathFinished = true };

        var next = path.Peek();

        return new Perception
        {
            PathFinished = false,
            NextCell = next,
            NextIsBlocked =
                next.IsShelf ||
                next.OccupyingRobot != null
        };
    }

    MoveIntent Stay() =>
        new MoveIntent { Robot = this, From = CurrentCell, To = null };

    // =====================
    // CALLED BY WAREHOUSE
    // =====================

    public void CommitMove(Cell newCell)
    {
        OnMoved?.Invoke();
        CurrentCell = newCell;

        if (path.Count > 0)
            path.Dequeue();

        // ======================
        // PICKUP 
        // ======================

        if (!IsCarrying && newCell.OccupyingCrate != null)
        {
            CarriedCrate = newCell.OccupyingCrate;
            newCell.OccupyingCrate = null;
            CarriedCrate.CurrentCell = null;
        }

        // ======================
        // DROP
        // ======================

        if (IsCarrying && newCell.CanStack())
        {
            newCell.StackHeight++;
            CarriedCrate.CurrentCell = null;
            CarriedCrate = null;
        }
    }

    public void ClearPath()
    {
        path.Clear();
        goalCell = null;
        State = RobotState.Idle;
    }
}