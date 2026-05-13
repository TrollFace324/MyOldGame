namespace MyGame
{
    // Хранит две клетки, между которыми стоит забор
    public readonly struct MapFence(CellPosition firstCell, CellPosition secondCell)
    {
        public CellPosition FirstCell { get; } = firstCell;
        public CellPosition SecondCell { get; } = secondCell;

        public bool IsVertical =>
            FirstCell.Row == SecondCell.Row &&
            FirstCell.Column != SecondCell.Column;

        public int BoundaryColumn =>
            FirstCell.Column > SecondCell.Column ? FirstCell.Column : SecondCell.Column;

        public int BoundaryRow =>
            FirstCell.Row > SecondCell.Row ? FirstCell.Row : SecondCell.Row;

        // Проверяет, стоит ли забор между двумя клетками
        public bool IsBetween(CellPosition first, CellPosition second) =>
            AreSameCell(FirstCell, first) && AreSameCell(SecondCell, second) ||
            AreSameCell(FirstCell, second) && AreSameCell(SecondCell, first);

        // Проверяет, совпадают ли две клетки
        private static bool AreSameCell(CellPosition first, CellPosition second) =>
            first.Column == second.Column &&
            first.Row == second.Row;
    }
}
