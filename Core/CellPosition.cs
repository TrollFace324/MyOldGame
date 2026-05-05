using System;

namespace MyGame
{
    public readonly struct CellPosition(int column, int row)
    {
        public int Column { get; } = column;
        public int Row { get; } = row;
    }
}
