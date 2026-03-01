public class Cell
{
    public int Row;
    public int Col;

    public RobotAgent OccupyingRobot;

    // loose crate on floor (before pickup)
    public Crate OccupyingCrate;

    // warehouse structure
    public bool IsShelf;

    // drop zone stacking
    public bool IsDropZone;
    public int StackHeight; // how many crates stacked here (0–5)

    // ======================
    // STATE HELPERS
    // ======================

    public bool IsFree()
    {
        return OccupyingRobot == null &&
               OccupyingCrate == null &&
               !IsShelf &&
               !IsDropZone;
    }

    public bool HasLooseCrate()
    {
        return OccupyingCrate != null;
    }

    public bool CanStack()
    {
        return IsDropZone && StackHeight < 5;
    }
}