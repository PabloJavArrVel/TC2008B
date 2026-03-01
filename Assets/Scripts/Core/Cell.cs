public class Cell
{
    public int Row;
    public int Col;

    public RobotAgent OccupyingRobot;
    public Crate OccupyingCrate;
    public bool IsShelf;

    public bool IsFree()
    {
        return OccupyingRobot == null &&
               OccupyingCrate == null &&
               !IsShelf;
    }
}