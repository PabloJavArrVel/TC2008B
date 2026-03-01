using System;
using System.Collections.Generic;

public class RobotAgent
{
    public int Id { get; }
    public Cell CurrentCell { get; private set; }

    public RobotState State { get; private set; } = RobotState.Idle;

    public bool IsCarrying { get; private set; }

    private Queue<Cell> path = new();

    public event Action<RobotAgent> OnTaskFinished;

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
            return Stay();

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
        path.Dequeue();

        // PICKUP
        if (!IsCarrying && newCell.OccupyingCrate != null)
        {
            var crate = newCell.OccupyingCrate;
            newCell.OccupyingCrate = null;
            IsCarrying = true;
        }

        // DROP
        if (IsCarrying && newCell.CanStack())
        {
            newCell.StackHeight++;
            IsCarrying = false;
        }
    }
}