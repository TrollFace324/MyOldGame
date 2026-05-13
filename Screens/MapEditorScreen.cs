using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MyGame.Services;

namespace MyGame.Screens
{
    public class MapEditorScreen : UserControl
    {
        private readonly MainForm mainForm;
        private readonly GameMap map = MapStorageService.LoadOrCreateDefault();
        private readonly Button stoneButton;
        private readonly Button waterButton;
        private readonly Button bushButton;
        private readonly Button fenceButton;
        private readonly Control saveButton;
        private readonly Control backButton;
        private readonly Image? backgroundImage;
        private readonly List<Image> buttonImages = [];
        private MapEditorSelection? selectedElement;

        private const float MapPadding = 32f;
        private const int MenuWidth = 180;

        // Создает редактор карты и его кнопки
        public MapEditorScreen(MainForm mainForm)
        {
            this.mainForm = mainForm;
            BackColor = Color.White;
            DoubleBuffered = true;
            backgroundImage = ImageAssetService.LoadImage("assets", "background", "main-menu.png");

            stoneButton = CreateToolButton("Камень");
            waterButton = CreateToolButton("Вода");
            bushButton = CreateToolButton("Кусты");
            fenceButton = CreateToolButton("Забор");

            saveButton = CreateImageButton("assets", "buttons", "save.png");
            backButton = CreateImageButton("assets", "buttons", "back.png");

            stoneButton.Click += (_, _) => ApplyTerrain(TerrainType.Stone);
            waterButton.Click += (_, _) => ApplyTerrain(TerrainType.Water);
            bushButton.Click += (_, _) => ApplyTerrain(TerrainType.Bush);
            fenceButton.Click += (_, _) => ApplyFence();
            saveButton.Click += (_, _) => SaveMap();
            backButton.Click += (_, _) => mainForm.ShowMainMenu();

            Controls.Add(stoneButton);
            Controls.Add(waterButton);
            Controls.Add(bushButton);
            Controls.Add(fenceButton);
            Controls.Add(saveButton);
            Controls.Add(backButton);

            Resize += (_, _) => LayoutMenu();
            MouseClick += OnMouseClick;
            LayoutMenu();
        }

        // Рисует карту редактора и текущее выделение
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            DrawEditorBackground(e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var mapBounds = GetMapBounds();
            var cellSize = mapBounds.Width / GameMap.Width;

            DrawMap(e.Graphics, mapBounds, cellSize);
            DrawSelectedElement(e.Graphics, mapBounds, cellSize);
        }

        // Обрабатывает выбор клетки или линии на карте
        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            var mapBounds = GetMapBounds();
            var cellSize = mapBounds.Width / GameMap.Width;

            if (!TryGetElementAt(e.Location, mapBounds, cellSize, out var element))
                return;

            if (selectedElement.HasValue && selectedElement.Value.IsSame(element))
            {
                if (HasObstacle(element))
                {
                    RemoveObstacle(element);
                    selectedElement = null;
                }
                else
                {
                    selectedElement = null;
                }
            }
            else
            {
                selectedElement = element;
            }

            UpdateToolButtons();
            Invalidate();
        }

        // Создает кнопку панели инструментов
        private static Button CreateToolButton(string text) =>
            new()
            {
                Text = text,
                Size = new Size(140, 40),
                UseVisualStyleBackColor = true
            };

        private Control CreateImageButton(params string[] relativePathParts)
        {
            var image = ImageAssetService.LoadImage(relativePathParts);

            if (image == null)
            {
                return new Panel
                {
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Size = new Size(140, 40)
                };
            }

            buttonImages.Add(image);

            return new PictureBox
            {
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Image = image,
                Size = GetImageButtonSize(image),
                SizeMode = PictureBoxSizeMode.Zoom
            };
        }

        private static Size GetImageButtonSize(Image image)
        {
            const int targetWidth = 140;
            var targetHeight = Math.Max(32, (int)Math.Round(targetWidth * (image.Height / (float)image.Width)));
            return new Size(targetWidth, targetHeight);
        }

        private void DrawEditorBackground(Graphics graphics)
        {
            if (backgroundImage == null)
            {
                graphics.Clear(Color.FromArgb(32, 32, 32));
                return;
            }

            DrawCoverImage(graphics, backgroundImage, ClientRectangle);

            using var overlayBrush = new SolidBrush(Color.FromArgb(132, 0, 0, 0));
            graphics.FillRectangle(overlayBrush, ClientRectangle);
        }

        private static void DrawCoverImage(Graphics graphics, Image image, Rectangle bounds)
        {
            var imageAspect = (float)image.Width / image.Height;
            var boundsAspect = bounds.Width / (float)Math.Max(1, bounds.Height);
            RectangleF destination;

            if (boundsAspect > imageAspect)
            {
                var height = bounds.Width / imageAspect;
                destination = new RectangleF(bounds.X, bounds.Y + (bounds.Height - height) / 2f, bounds.Width, height);
            }
            else
            {
                var width = bounds.Height * imageAspect;
                destination = new RectangleF(bounds.X + (bounds.Width - width) / 2f, bounds.Y, width, bounds.Height);
            }

            graphics.DrawImage(image, destination);
        }

        // Расставляет кнопки редактора слева
        private void LayoutMenu()
        {
            var x = 20;
            var y = 24;
            var gap = 12;

            stoneButton.Location = new Point(x, y);
            waterButton.Location = new Point(x, y += stoneButton.Height + gap);
            bushButton.Location = new Point(x, y += waterButton.Height + gap);
            fenceButton.Location = new Point(x, y += bushButton.Height + gap);

            var backY = Height - backButton.Height - 20;
            var saveY = backY - saveButton.Height - gap;
            saveButton.Location = new Point(x, saveY);
            backButton.Location = new Point(x, backY);
        }

        // Сохраняет карту в файл
        private void SaveMap()
        {
            var path = MapStorageService.Save(map);
            MessageBox.Show(this, $"Карта сохранена:\n{path}", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Ставит или убирает препятствие в выбранной клетке
        private void ApplyTerrain(TerrainType terrain)
        {
            if (!selectedElement.HasValue || selectedElement.Value.Kind != MapElementKind.Cell)
                return;

            var cell = selectedElement.Value.Cell;

            if (map.GetTerrain(cell) == terrain)
                map.ClearTerrain(cell);
            else
                map.SetTerrain(cell, terrain);

            UpdateToolButtons();
            Invalidate();
        }

        // Ставит или убирает забор на выбранной линии
        private void ApplyFence()
        {
            if (!selectedElement.HasValue || selectedElement.Value.Kind != MapElementKind.Fence)
                return;

            var fence = selectedElement.Value.Fence;

            if (map.HasFenceBetween(fence.FirstCell, fence.SecondCell))
                map.RemoveFenceBetween(fence.FirstCell, fence.SecondCell);
            else
                map.AddFenceBetween(fence.FirstCell, fence.SecondCell);

            UpdateToolButtons();
            Invalidate();
        }

        // Удаляет препятствие с выбранного элемента
        private void RemoveObstacle(MapEditorSelection element)
        {
            if (element.Kind == MapElementKind.Cell)
            {
                map.ClearTerrain(element.Cell);
                return;
            }

            map.RemoveFenceBetween(element.Fence.FirstCell, element.Fence.SecondCell);
        }

        // Проверяет, есть ли препятствие на выбранном элементе
        private bool HasObstacle(MapEditorSelection element)
        {
            if (element.Kind == MapElementKind.Cell)
                return map.GetTerrain(element.Cell) != TerrainType.Empty;

            return map.HasFenceBetween(element.Fence.FirstCell, element.Fence.SecondCell);
        }

        // Обновляет подсветку кнопок инструментов
        private void UpdateToolButtons()
        {
            ResetToolButton(stoneButton);
            ResetToolButton(waterButton);
            ResetToolButton(bushButton);
            ResetToolButton(fenceButton);

            if (!selectedElement.HasValue)
                return;

            var element = selectedElement.Value;

            if (element.Kind == MapElementKind.Fence)
            {
                if (map.HasFenceBetween(element.Fence.FirstCell, element.Fence.SecondCell))
                    MarkToolButton(fenceButton);

                return;
            }

            switch (map.GetTerrain(element.Cell))
            {
                case TerrainType.Stone:
                    MarkToolButton(stoneButton);
                    break;
                case TerrainType.Water:
                    MarkToolButton(waterButton);
                    break;
                case TerrainType.Bush:
                    MarkToolButton(bushButton);
                    break;
            }
        }

        // Возвращает обычный вид кнопке инструмента
        private static void ResetToolButton(Button button)
        {
            button.BackColor = SystemColors.Control;
            button.UseVisualStyleBackColor = true;
        }

        // Подсвечивает выбранную кнопку инструмента
        private static void MarkToolButton(Button button)
        {
            button.UseVisualStyleBackColor = false;
            button.BackColor = Color.FromArgb(214, 214, 214);
        }

        // Рисует карту внутри редактора
        private void DrawMap(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            using var mapBrush = new SolidBrush(Color.FromArgb(244, 238, 214));
            using var spawnBrush = new SolidBrush(Color.FromArgb(247, 219, 92));
            using var stoneBrush = new SolidBrush(Color.FromArgb(150, 150, 150));
            using var waterBrush = new SolidBrush(Color.FromArgb(112, 198, 232));
            using var bushBrush = new SolidBrush(Color.FromArgb(78, 151, 83));
            using var cellPen = new Pen(Color.FromArgb(198, 187, 145), 1f);
            using var borderPen = new Pen(Color.FromArgb(110, 88, 56), 3f);

            graphics.FillRectangle(mapBrush, mapBounds);

            for (var row = 0; row < GameMap.Height; row++)
            {
                for (var column = 0; column < GameMap.Width; column++)
                {
                    var cellX = mapBounds.X + column * cellSize;
                    var cellY = mapBounds.Y + row * cellSize;
                    var cell = new CellPosition(column, row);

                    if (map.IsSpawnCell(cell))
                        graphics.FillRectangle(spawnBrush, cellX, cellY, cellSize, cellSize);

                    var terrainBrush = map.GetTerrain(cell) switch
                    {
                        TerrainType.Stone => stoneBrush,
                        TerrainType.Water => waterBrush,
                        TerrainType.Bush => bushBrush,
                        _ => null
                    };

                    if (terrainBrush != null)
                        graphics.FillRectangle(terrainBrush, cellX, cellY, cellSize, cellSize);

                    graphics.DrawRectangle(cellPen, cellX, cellY, cellSize, cellSize);
                }
            }

            DrawFences(graphics, mapBounds, cellSize);
            graphics.DrawRectangle(borderPen, mapBounds.X, mapBounds.Y, mapBounds.Width, mapBounds.Height);
        }

        // Рисует заборы в редакторе
        private void DrawFences(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            using var fencePen = new Pen(Color.FromArgb(128, 76, 34), Math.Max(4f, cellSize * 0.08f))
            {
                StartCap = LineCap.Square,
                EndCap = LineCap.Square
            };

            foreach (var fence in map.Fences)
                DrawFenceLine(graphics, mapBounds, cellSize, fence, fencePen);
        }

        // Рисует подсветку выбранной клетки или линии
        private void DrawSelectedElement(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            if (!selectedElement.HasValue)
                return;

            var element = selectedElement.Value;
            var color = HasObstacle(element)
                ? Color.FromArgb(140, 130, 130, 130)
                : Color.FromArgb(90, 220, 48, 48);

            if (element.Kind == MapElementKind.Cell)
            {
                using var brush = new SolidBrush(color);
                var cell = element.Cell;
                graphics.FillRectangle(
                    brush,
                    mapBounds.X + cell.Column * cellSize,
                    mapBounds.Y + cell.Row * cellSize,
                    cellSize,
                    cellSize);
                return;
            }

            using var pen = new Pen(color, Math.Max(6f, cellSize * 0.12f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            DrawFenceLine(graphics, mapBounds, cellSize, element.Fence, pen);
        }

        // Рисует одну линию забора
        private static void DrawFenceLine(
            Graphics graphics,
            RectangleF mapBounds,
            float cellSize,
            MapFence fence,
            Pen pen)
        {
            if (fence.IsVertical)
            {
                var x = mapBounds.X + fence.BoundaryColumn * cellSize;
                var y = mapBounds.Y + fence.FirstCell.Row * cellSize;
                graphics.DrawLine(pen, x, y, x, y + cellSize);
                return;
            }

            var horizontalX = mapBounds.X + fence.FirstCell.Column * cellSize;
            var horizontalY = mapBounds.Y + fence.BoundaryRow * cellSize;
            graphics.DrawLine(pen, horizontalX, horizontalY, horizontalX + cellSize, horizontalY);
        }

        // Считает размер и положение карты редактора
        private RectangleF GetMapBounds()
        {
            var availableWidth = Math.Max(120f, Width - MenuWidth - MapPadding * 2f);
            var availableHeight = Math.Max(120f, Height - MapPadding * 2f);
            var cellSize = Math.Min(availableWidth / GameMap.Width, availableHeight / GameMap.Height);
            var mapWidth = cellSize * GameMap.Width;
            var mapHeight = cellSize * GameMap.Height;

            return new RectangleF(
                MenuWidth + (Width - MenuWidth - mapWidth) / 2f,
                (Height - mapHeight) / 2f,
                mapWidth,
                mapHeight);
        }

        // Определяет клетку или линию под курсором
        private bool TryGetElementAt(Point location, RectangleF mapBounds, float cellSize, out MapEditorSelection element)
        {
            element = default;

            if (!mapBounds.Contains(location.X, location.Y))
                return false;

            var mapX = (location.X - mapBounds.X) / cellSize;
            var mapY = (location.Y - mapBounds.Y) / cellSize;
            var lineTolerance = Math.Min(8f, cellSize * 0.12f);
            var verticalColumn = (int)MathF.Round(mapX);
            var horizontalRow = (int)MathF.Round(mapY);
            var verticalDistance = MathF.Abs(mapX - verticalColumn) * cellSize;
            var horizontalDistance = MathF.Abs(mapY - horizontalRow) * cellSize;
            var canSelectVerticalLine =
                verticalColumn > 0 &&
                verticalColumn < GameMap.Width &&
                verticalDistance <= lineTolerance &&
                mapY >= 0f &&
                mapY < GameMap.Height;
            var canSelectHorizontalLine =
                horizontalRow > 0 &&
                horizontalRow < GameMap.Height &&
                horizontalDistance <= lineTolerance &&
                mapX >= 0f &&
                mapX < GameMap.Width;

            if (canSelectVerticalLine && (!canSelectHorizontalLine || verticalDistance <= horizontalDistance))
            {
                var row = (int)MathF.Floor(mapY);
                element = MapEditorSelection.ForFence(
                    new MapFence(
                        new CellPosition(verticalColumn - 1, row),
                        new CellPosition(verticalColumn, row)));
                return true;
            }

            if (canSelectHorizontalLine)
            {
                var column = (int)MathF.Floor(mapX);
                element = MapEditorSelection.ForFence(
                    new MapFence(
                        new CellPosition(column, horizontalRow - 1),
                        new CellPosition(column, horizontalRow)));
                return true;
            }

            var cellColumn = (int)MathF.Floor(mapX);
            var cellRow = (int)MathF.Floor(mapY);
            var cell = new CellPosition(cellColumn, cellRow);

            if (!map.IsInside(cell))
                return false;

            element = MapEditorSelection.ForCell(cell);
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                backgroundImage?.Dispose();

                foreach (var image in buttonImages)
                    image.Dispose();
            }

            base.Dispose(disposing);
        }

        private enum MapElementKind
        {
            Cell,
            Fence
        }

        private readonly struct MapEditorSelection
        {
            // Создает выделение клетки или забора
            private MapEditorSelection(MapElementKind kind, CellPosition cell, MapFence fence)
            {
                Kind = kind;
                Cell = cell;
                Fence = fence;
            }

            public MapElementKind Kind { get; }
            public CellPosition Cell { get; }
            public MapFence Fence { get; }

            // Создает выделение клетки
            public static MapEditorSelection ForCell(CellPosition cell) =>
                new(MapElementKind.Cell, cell, default);

            // Создает выделение забора
            public static MapEditorSelection ForFence(MapFence fence) =>
                new(MapElementKind.Fence, default, fence);

            // Проверяет, совпадают ли два выделения
            public bool IsSame(MapEditorSelection other)
            {
                if (Kind != other.Kind)
                    return false;

                if (Kind == MapElementKind.Cell)
                    return AreSameCell(Cell, other.Cell);

                return Fence.IsBetween(other.Fence.FirstCell, other.Fence.SecondCell);
            }

            // Проверяет, совпадают ли две клетки
            private static bool AreSameCell(CellPosition first, CellPosition second) =>
                first.Column == second.Column &&
                first.Row == second.Row;
        }
    }
}
