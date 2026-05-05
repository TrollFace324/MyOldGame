using System;

namespace MyGame
{
    public class Tank(CellPosition startCell)
    {
        public CellPosition CurrentCell { get; private set; } = startCell;
        public MoveDirection FacingDirection { get; private set; } = MoveDirection.Up;

        public void MoveTo(CellPosition nextCell)
        {
            CurrentCell = nextCell;
        }

        public void RotateTo(MoveDirection direction)
        {
            if (direction == MoveDirection.None)
                return;

            FacingDirection = direction;
        }
    }
}
