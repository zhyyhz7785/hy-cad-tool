namespace HyCADTool.HelpClass
{
    public class RowColValue<T>
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public T Value { get; set; }
        public RowColValue(int row, int col, T value)
        {
            Row = row;
            Col = col;
            Value = value;
        }
    }
}
