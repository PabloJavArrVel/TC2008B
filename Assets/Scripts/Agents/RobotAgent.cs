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

    // deadlock prevention
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
        return Decide(p);
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

    private MoveIntent Decide(Perception p)
    {
        if (p.PathFinished)
        {
            if (State == RobotState.Tasked)
            {
                State = RobotState.Idle;
                OnTaskFinished?.Invoke(this);
            }

            return Stay();
        }

        if (p.NextIsBlocked)
        {
            blockedSteps++;

            // simple deadlock prevention
            if (blockedSteps > 3)
            {
                State = RobotState.Idle;
                OnTaskFinished?.Invoke(this);
            }

            return Stay();
        }

        blockedSteps = 0;

        return new MoveIntent
        {
            Robot = this,
            From = CurrentCell,
            To = p.NextCell
        };
    }

    MoveIntent Stay() =>
        new MoveIntent { Robot = this, From = CurrentCell, To = null };

    // =====================
    // CALLED BY WAREHOUSE
    // =====================

    public void CommitMove(Cell newCell)
    {
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
        }

        // ======================
        // DROP
        // ======================

        if (IsCarrying && newCell.CanStack())
        {
            newCell.StackHeight++;
            CarriedCrate.CurrentCell = newCell;
            CarriedCrate = null;
        }
    }
}