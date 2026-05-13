using System;
using System.Collections.Generic;

namespace MyGame
{
    public class GameMap
    {
        public const int Width = 10;
        public const int Height = 10;

        private readonly TerrainType[,] terrain = new TerrainType[Width, Height];
        private readonly List<MapFence> fences = [];
        private readonly CellPosition[] playerSpawnCells =
        [
            new CellPosition(3, 9),
            new CellPosition(4, 9)
        ];
        private readonly CellPosition[] enemySpawnCells =
        [
            new CellPosition(0, 3),
            new CellPosition(9, 2),
            new CellPosition(2, 0)
        ];

        // Создает карту с препятствиями по умолчанию
        public GameMap()
            : this(true)
        {
        }

        // Создает карту и решает, нужны ли препятствия по умолчанию
        public GameMap(bool includeDefaultObstacles)
        {
            if (includeDefaultObstacles)
                AddDefaultObstacles();
        }

        // Добавляет стартовый набор препятствий на карту
        private void AddDefaultObstacles()
        {
            SetTerrain(new CellPosition(4, 4), TerrainType.Stone);
            SetTerrain(new CellPosition(5, 4), TerrainType.Water);
            SetTerrain(new CellPosition(4, 5), TerrainType.Bush);
            fences.Add(new MapFence(new CellPosition(5, 5), new CellPosition(6, 5)));
        }

        public IReadOnlyList<MapFence> Fences => fences;
        public IReadOnlyList<CellPosition> PlayerSpawnCells => playerSpawnCells;
        public IReadOnlyList<CellPosition> EnemySpawnCells => enemySpawnCells;

        // Возвращает тип клетки карты
        public TerrainType GetTerrain(CellPosition cell) =>
            IsInside(cell)
                ? terrain[cell.Column, cell.Row]
                : TerrainType.Empty;

        // Возвращает тип клетки по точке на карте
        public TerrainType GetTerrainAt(MapPoint point)
        {
            var cell = GetCellAt(point);
            return GetTerrain(cell);
        }

        // Проверяет, заблокирована ли клетка камнем
        public bool IsBlocked(CellPosition cell) =>
            GetTerrain(cell) == TerrainType.Stone;

        // Проверяет, является ли клетка точкой спавна
        public bool IsSpawnCell(CellPosition cell) =>
            ContainsCell(playerSpawnCells, cell) ||
            ContainsCell(enemySpawnCells, cell);

        // Проверяет, находится ли клетка внутри карты
        public bool IsInside(CellPosition cell) =>
            cell.Column >= 0 &&
            cell.Column < Width &&
            cell.Row >= 0 &&
            cell.Row < Height;

        // Ломает забор между двумя клетками, если он есть
        public bool TryBreakFenceBetween(CellPosition first, CellPosition second)
        {
            for (var i = fences.Count - 1; i >= 0; i--)
            {
                if (!fences[i].IsBetween(first, second))
                    continue;

                fences.RemoveAt(i);
                return true;
            }

            return false;
        }

        // Ломает забор, который пересек снаряд
        public bool TryBreakFenceCrossedBySegment(float startX, float startY, float endX, float endY)
        {
            for (var i = fences.Count - 1; i >= 0; i--)
            {
                if (!DoesSegmentCrossFence(startX, startY, endX, endY, fences[i]))
                    continue;

                fences.RemoveAt(i);
                return true;
            }

            return false;
        }

        // Устанавливает препятствие или пустоту в клетке
        public void SetTerrain(CellPosition cell, TerrainType type)
        {
            if (IsInside(cell))
                terrain[cell.Column, cell.Row] = type;
        }

        // Очищает клетку от препятствия
        public void ClearTerrain(CellPosition cell)
        {
            SetTerrain(cell, TerrainType.Empty);
        }

        // Проверяет, есть ли забор между клетками
        public bool HasFenceBetween(CellPosition first, CellPosition second)
        {
            foreach (var fence in fences)
            {
                if (fence.IsBetween(first, second))
                    return true;
            }

            return false;
        }

        // Добавляет забор между соседними клетками
        public void AddFenceBetween(CellPosition first, CellPosition second)
        {
            if (!IsInside(first) || !IsInside(second) || HasFenceBetween(first, second))
                return;

            fences.Add(new MapFence(first, second));
        }

        // Удаляет забор между клетками
        public void RemoveFenceBetween(CellPosition first, CellPosition second)
        {
            for (var i = fences.Count - 1; i >= 0; i--)
            {
                if (fences[i].IsBetween(first, second))
                    fences.RemoveAt(i);
            }
        }

        // Проверяет, есть ли клетка в списке
        private static bool ContainsCell(IEnumerable<CellPosition> cells, CellPosition cell)
        {
            foreach (var candidate in cells)
            {
                if (candidate.Column == cell.Column && candidate.Row == cell.Row)
                    return true;
            }

            return false;
        }

        // Переводит точку карты в клетку
        private static CellPosition GetCellAt(MapPoint point) =>
            new CellPosition((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));

        // Проверяет, пересекает ли отрезок забор
        private static bool DoesSegmentCrossFence(float startX, float startY, float endX, float endY, MapFence fence)
        {
            if (fence.IsVertical)
                return DoesSegmentCrossVerticalFence(startX, startY, endX, endY, fence);

            return DoesSegmentCrossHorizontalFence(startX, startY, endX, endY, fence);
        }

        // Проверяет пересечение отрезка с вертикальным забором
        private static bool DoesSegmentCrossVerticalFence(float startX, float startY, float endX, float endY, MapFence fence)
        {
            var boundaryX = fence.BoundaryColumn;
            var minY = fence.FirstCell.Row;
            var maxY = fence.FirstCell.Row + 1f;

            if (MathF.Abs(endX - startX) < float.Epsilon)
                return MathF.Abs(startX - boundaryX) < float.Epsilon && IsBetween(startY, minY, maxY);

            var progress = (boundaryX - startX) / (endX - startX);

            if (progress < 0f || progress > 1f)
                return false;

            var crossingY = startY + (endY - startY) * progress;
            return IsBetween(crossingY, minY, maxY);
        }

        // Проверяет пересечение отрезка с горизонтальным забором
        private static bool DoesSegmentCrossHorizontalFence(float startX, float startY, float endX, float endY, MapFence fence)
        {
            var boundaryY = fence.BoundaryRow;
            var minX = fence.FirstCell.Column;
            var maxX = fence.FirstCell.Column + 1f;

            if (MathF.Abs(endY - startY) < float.Epsilon)
                return MathF.Abs(startY - boundaryY) < float.Epsilon && IsBetween(startX, minX, maxX);

            var progress = (boundaryY - startY) / (endY - startY);

            if (progress < 0f || progress > 1f)
                return false;

            var crossingX = startX + (endX - startX) * progress;
            return IsBetween(crossingX, minX, maxX);
        }

        // Проверяет, находится ли число в диапазоне
        private static bool IsBetween(float value, float min, float max) =>
            value >= min &&
            value <= max;
    }
}
