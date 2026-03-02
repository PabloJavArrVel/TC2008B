public class Crate
{
    public int Id;
    public Cell CurrentCell;
    public bool IsDelivered { get; set; }

    public Crate(int id, Cell cell)
    {
        Id = id;
        CurrentCell = cell;
        cell.OccupyingCrate = this;
    }
}