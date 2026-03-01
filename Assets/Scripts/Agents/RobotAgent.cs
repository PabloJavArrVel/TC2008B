public enum RobotState
{
    Idle,
    Moving
}

public class RobotAgent
{
    public int Id;
    public Cell CurrentCell;

    public RobotAgent(int id, Cell startCell)
    {
        Id = id;
        CurrentCell = startCell;
        startCell.OccupyingRobot = this;
    }

    public void MoveTo(Cell target)
    {
        CurrentCell.OccupyingRobot = null;
        target.OccupyingRobot = this;
        CurrentCell = target;
    }
}