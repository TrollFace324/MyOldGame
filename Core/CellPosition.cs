namespace MyGame
{
    // Хранит координаты клетки карты
    public readonly struct CellPosition(int column, int row)
    {
        public int Column { get; } = column;
        public int Row { get; } = row;
    }
}
