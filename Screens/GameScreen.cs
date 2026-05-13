using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using MyGame;
using MyGame.Services;

namespace MyGame.Screens
{
    public class GameScreen : UserControl
    {
        private readonly MainForm mainForm;
        private readonly GameSession session;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Dictionary<TankLevel, Image> playerTankSprites;
        private readonly Dictionary<TankLevel, Image> enemyTankSprites;
        private readonly Dictionary<TerrainType, Image> terrainSprites;
        private readonly GameAudioService audioService;
        private readonly Image? gameOverImage;
        private DateTime lastUpdateTime;
        private DateTime? gameOverStartedAt;
        private const float MapPadding = 32f;
        private const float StatsPanelWidth = 160f;
        private const float StatsMapGap = 24f;
        private const float OccupiedBushOpacity = 0.3f;
        private const double GameOverSeconds = 3d;

        // Создает игровой экран, сессию и таймер обновления
        public GameScreen(MainForm mainForm)
        {
            this.mainForm = mainForm;
            BackColor = Color.White;
            DoubleBuffered = true;
            TabStop = true;

            session = new GameSession(new RandomSpawnService(), MapStorageService.LoadOrCreateDefault());
            audioService = new GameAudioService();
            session.ProjectileFired += audioService.PlayShooting;
            session.TankHit += audioService.PlayTankHit;
            playerTankSprites = LoadTankSprites("tank");
            enemyTankSprites = LoadTankSprites("tank-r");
            terrainSprites = LoadTerrainSprites();
            gameOverImage = ImageAssetService.LoadImage("assets", "background", "game-over.png");
            audioService.PlayBackgroundMusic();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += OnTick;
            lastUpdateTime = DateTime.Now;
            timer.Start();

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }
            
        // Обновляет игру по таймеру и просит перерисовать экран
        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var deltaTime = (float)(now - lastUpdateTime).TotalSeconds;
            lastUpdateTime = now;

            if (gameOverStartedAt.HasValue)
            {
                if ((now - gameOverStartedAt.Value).TotalSeconds >= GameOverSeconds)
                {
                    timer.Stop();
                    mainForm.ShowMainMenu();
                    return;
                }

                Invalidate();
                return;
            }

            session.Update(deltaTime);

            if (session.ShouldReturnToMainMenu)
            {
                gameOverStartedAt = now;
                Invalidate();
                return;
            }

            Invalidate();
        }

        // Передает нажатия клавиш в игровую сессию
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                case Keys.Up:
                    session.PressDirection(MoveDirection.Up);
                    break;
                case Keys.S:
                case Keys.Down:
                    session.PressDirection(MoveDirection.Down);
                    break;
                case Keys.A:
                case Keys.Left:
                    session.PressDirection(MoveDirection.Left);
                    break;
                case Keys.D:
                case Keys.Right:
                    session.PressDirection(MoveDirection.Right);
                    break;
                case Keys.Space:
                    session.PressFire();
                    break;
                case Keys.Escape:
                    timer.Stop();
                    mainForm.ShowMainMenu();
                    break;
            }
        }

        // Передает отпускания клавиш в игровую сессию
        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                case Keys.Up:
                    session.ReleaseDirection(MoveDirection.Up);
                    break;
                case Keys.S:
                case Keys.Down:
                    session.ReleaseDirection(MoveDirection.Down);
                    break;
                case Keys.A:
                case Keys.Left:
                    session.ReleaseDirection(MoveDirection.Left);
                    break;
                case Keys.D:
                case Keys.Right:
                    session.ReleaseDirection(MoveDirection.Right);
                    break;
                case Keys.Space:
                    session.ReleaseFire();
                    break;
            }
        }

        // Рисует текущее состояние игры
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var mapBounds = GetMapBounds();
            var cellSize = mapBounds.Width / GameMap.Width;

            DrawMap(e.Graphics, mapBounds, cellSize);
            DrawLevelBonus(e.Graphics, mapBounds, cellSize);
            DrawTank(
                e.Graphics,
                mapBounds,
                cellSize,
                session.PlayerTank,
                Color.FromArgb(48, 120, 84),
                Color.FromArgb(28, 64, 46),
                Color.FromArgb(218, 206, 154),
                playerTankSprites);
            DrawEnemyTanks(e.Graphics, mapBounds, cellSize);
            DrawProjectiles(e.Graphics, mapBounds, cellSize);
            DrawPlayerStats(e.Graphics, mapBounds, cellSize);

            if (gameOverStartedAt.HasValue)
                DrawGameOverOverlay(e.Graphics);
        }

        // Рисует поле, клетки, препятствия и спавны
        private void DrawMap(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            using var mapBrush = new SolidBrush(Color.FromArgb(244, 238, 214));
            using var spawnBrush = new SolidBrush(Color.FromArgb(247, 219, 92));
            using var spawnOverlayBrush = new SolidBrush(Color.FromArgb(96, 247, 219, 92));
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
                    var terrain = session.Map.GetTerrain(cell);
                    var cellBounds = new RectangleF(cellX, cellY, cellSize, cellSize);
                    var terrainOpacity = terrain == TerrainType.Bush && IsAnyTankCenteredInCell(cell)
                        ? OccupiedBushOpacity
                        : 1f;
                    var hasDirtSprite = DrawTerrainSprite(graphics, cellBounds, TerrainType.Empty);

                    if (!hasDirtSprite)
                        graphics.FillRectangle(mapBrush, cellBounds);

                    if (terrain != TerrainType.Empty && !DrawTerrainSprite(graphics, cellBounds, terrain, terrainOpacity))
                    {
                        var terrainColor = terrain switch
                        {
                            TerrainType.Stone => Color.FromArgb(150, 150, 150),
                            TerrainType.Water => Color.FromArgb(112, 198, 232),
                            TerrainType.Bush => Color.FromArgb((int)(255 * terrainOpacity), 78, 151, 83),
                            _ => Color.Empty
                        };

                        if (!terrainColor.IsEmpty)
                        {
                            using var terrainBrush = new SolidBrush(terrainColor);
                            graphics.FillRectangle(terrainBrush, cellBounds);
                        }
                    }

                    if (session.Map.IsSpawnCell(cell))
                    {
                        graphics.FillRectangle(hasDirtSprite ? spawnOverlayBrush : spawnBrush, cellBounds);
                    }

                    graphics.DrawRectangle(cellPen, cellX, cellY, cellSize, cellSize);
                }
            }

            DrawFences(graphics, mapBounds, cellSize);
            graphics.DrawRectangle(borderPen, mapBounds.X, mapBounds.Y, mapBounds.Width, mapBounds.Height);
        }

        private bool DrawTerrainSprite(Graphics graphics, RectangleF cellBounds, TerrainType terrain)
        {
            return DrawTerrainSprite(graphics, cellBounds, terrain, 1f);
        }

        private bool DrawTerrainSprite(Graphics graphics, RectangleF cellBounds, TerrainType terrain, float opacity)
        {
            var spriteKey = terrain == TerrainType.Empty ? TerrainType.Empty : terrain;

            if (!terrainSprites.TryGetValue(spriteKey, out var sprite))
                return false;

            var state = graphics.Save();
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;

            if (opacity >= 1f)
            {
                graphics.DrawImage(sprite, cellBounds);
            }
            else
            {
                using var attributes = new ImageAttributes();
                var matrix = new ColorMatrix
                {
                    Matrix33 = Math.Clamp(opacity, 0f, 1f)
                };

                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                graphics.DrawImage(
                    sprite,
                    Rectangle.Round(cellBounds),
                    0,
                    0,
                    sprite.Width,
                    sprite.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }

            graphics.Restore(state);
            return true;
        }

        private bool IsAnyTankCenteredInCell(CellPosition cell)
        {
            if (IsTankCenteredInCell(session.PlayerTank, cell))
                return true;

            foreach (var enemyTank in session.EnemyTanks)
            {
                if (IsTankCenteredInCell(enemyTank, cell))
                    return true;
            }

            return false;
        }

        private static bool IsTankCenteredInCell(Tank tank, CellPosition cell)
        {
            var tankCell = GetCellAt(tank.CenterPoint);
            return tankCell.Column == cell.Column && tankCell.Row == cell.Row;
        }

        private static CellPosition GetCellAt(MapPoint point) =>
            new((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));

        private void DrawGameOverOverlay(Graphics graphics)
        {
            using var overlayBrush = new SolidBrush(Color.FromArgb(172, 96, 96, 96));
            graphics.FillRectangle(overlayBrush, ClientRectangle);

            if (gameOverImage != null)
            {
                var imageBounds = GetCenteredImageBounds(gameOverImage, ClientRectangle, 0.55f);
                graphics.DrawImage(gameOverImage, imageBounds);
                return;
            }

            var fontSize = Math.Max(36f, Math.Min(96f, Width / 9f));
            using var font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            using var shadowBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var textBounds = new RectangleF(0, 0, Width, Height);
            var shadowBounds = new RectangleF(3, 3, Width, Height);

            graphics.DrawString("GAME OVER", font, shadowBrush, shadowBounds, format);
            graphics.DrawString("GAME OVER", font, textBrush, textBounds, format);
        }

        private static RectangleF GetCenteredImageBounds(Image image, Rectangle bounds, float maxBoundsShare)
        {
            var maxWidth = bounds.Width * maxBoundsShare;
            var maxHeight = bounds.Height * maxBoundsShare;
            var imageAspect = image.Width / (float)image.Height;
            var width = maxWidth;
            var height = width / imageAspect;

            if (height > maxHeight)
            {
                height = maxHeight;
                width = height * imageAspect;
            }

            return new RectangleF(
                bounds.X + (bounds.Width - width) / 2f,
                bounds.Y + (bounds.Height - height) / 2f,
                width,
                height);
        }

        // Рисует стрелку улучшения
        private void DrawLevelBonus(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            if (!session.LevelBonusCell.HasValue)
                return;

            var cell = session.LevelBonusCell.Value;
            var centerX = mapBounds.X + (cell.Column + 0.5f) * cellSize;
            var centerY = mapBounds.Y + (cell.Row + 0.5f) * cellSize;
            var arrowWidth = cellSize * 0.56f;
            var arrowHeight = cellSize * 0.68f;
            var shaftWidth = cellSize * 0.18f;
            var top = centerY - arrowHeight / 2f;
            var bottom = centerY + arrowHeight / 2f;
            var shoulderY = top + arrowHeight * 0.36f;

            PointF[] points =
            [
                new(centerX, top),
                new(centerX + arrowWidth / 2f, shoulderY),
                new(centerX + shaftWidth / 2f, shoulderY),
                new(centerX + shaftWidth / 2f, bottom),
                new(centerX - shaftWidth / 2f, bottom),
                new(centerX - shaftWidth / 2f, shoulderY),
                new(centerX - arrowWidth / 2f, shoulderY)
            ];

            using var path = new GraphicsPath();
            using var arrowBrush = new SolidBrush(Color.FromArgb(255, 238, 0));
            using var arrowPen = new Pen(Color.FromArgb(138, 116, 0), Math.Max(2f, cellSize * 0.04f));

            path.AddPolygon(points);
            graphics.FillPath(arrowBrush, path);
            graphics.DrawPath(arrowPen, path);
        }

        // Рисует все заборы на карте
        private void DrawFences(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            using var fencePen = new Pen(Color.FromArgb(128, 76, 34), Math.Max(4f, cellSize * 0.08f))
            {
                StartCap = LineCap.Square,
                EndCap = LineCap.Square
            };

            foreach (var fence in session.Map.Fences)
            {
                if (fence.IsVertical)
                {
                    var x = mapBounds.X + fence.BoundaryColumn * cellSize;
                    var y = mapBounds.Y + fence.FirstCell.Row * cellSize;
                    graphics.DrawLine(fencePen, x, y, x, y + cellSize);
                }
                else
                {
                    var x = mapBounds.X + fence.FirstCell.Column * cellSize;
                    var y = mapBounds.Y + fence.BoundaryRow * cellSize;
                    graphics.DrawLine(fencePen, x, y, x + cellSize, y);
                }
            }
        }

        // Рисует все вражеские танки
        private void DrawEnemyTanks(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            foreach (var enemyTank in session.EnemyTanks)
            {
                DrawTank(
                    graphics,
                    mapBounds,
                    cellSize,
                    enemyTank,
                    Color.FromArgb(164, 52, 52),
                    Color.FromArgb(96, 28, 28),
                    Color.FromArgb(232, 184, 122),
                    enemyTankSprites
                );
            }
        }

        // Рисует один танк с пушкой, центром, уровнем и здоровьем
        private void DrawTank(
            Graphics graphics,
            RectangleF mapBounds,
            float cellSize,
            Tank tank,
            Color bodyColor,
            Color outlineColor,
            Color hatchColor,
            IReadOnlyDictionary<TankLevel, Image> tankSprites
        )
        {
            var tankCenter = tank.CenterPoint;
            var bodySize = cellSize * 0.72f;
            var centerX = mapBounds.X + tankCenter.X * cellSize;
            var centerY = mapBounds.Y + tankCenter.Y * cellSize;

            if (tankSprites.TryGetValue(tank.Level, out var tankSprite))
            {
                var spriteHeight = cellSize * 0.86f;
                var spriteWidth = spriteHeight * tankSprite.Width / tankSprite.Height;
                var visualSize = Math.Max(spriteWidth, spriteHeight);

                DrawTankSprite(graphics, tankSprite, centerX, centerY, spriteWidth, spriteHeight, tank.FacingDirection);
                DrawTankHealthBar(graphics, centerX, centerY - visualSize / 2f, visualSize, tank);
                return;
            }

            var bodyRect = new RectangleF(
                centerX - bodySize / 2f,
                centerY - bodySize / 2f,
                bodySize,
                bodySize
            );

            var barrelEnd = GetBarrelEndPoint(centerX, centerY, cellSize, tank.FacingDirection);

            using var barrelPen = new Pen(Color.FromArgb(80, 80, 80), Math.Max(4f, cellSize * 0.16f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var bodyBrush = new SolidBrush(bodyColor);
            using var bodyOutlinePen = new Pen(outlineColor, 2f);
            using var hatchBrush = new SolidBrush(hatchColor);
            using var centerPointBrush = new SolidBrush(Color.FromArgb(24, 28, 24));

            graphics.DrawLine(barrelPen, centerX, centerY, barrelEnd.X, barrelEnd.Y);
            graphics.FillRectangle(bodyBrush, bodyRect);
            graphics.DrawRectangle(bodyOutlinePen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);

            var hatchSize = bodySize * 0.24f;
            graphics.FillEllipse(
                hatchBrush,
                centerX - hatchSize / 2f,
                centerY - hatchSize / 2f,
                hatchSize,
                hatchSize
            );

            var centerPointSize = Math.Max(4f, cellSize * 0.08f);
            graphics.FillEllipse(
                centerPointBrush,
                centerX - centerPointSize / 2f,
                centerY - centerPointSize / 2f,
                centerPointSize,
                centerPointSize
            );

            DrawTankLevelNumber(graphics, bodyRect, tank);
            DrawTankHealthBar(graphics, centerX, centerY - bodySize / 2f, bodySize, tank);
        }

        // Draws a level sprite rotated toward the tank direction.
        private static void DrawTankSprite(
            Graphics graphics,
            Image sprite,
            float centerX,
            float centerY,
            float spriteWidth,
            float spriteHeight,
            MoveDirection direction
        )
        {
            var state = graphics.Save();
            graphics.TranslateTransform(centerX, centerY);
            graphics.RotateTransform(GetTankRotationAngle(direction));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(sprite, -spriteWidth / 2f, -spriteHeight / 2f, spriteWidth, spriteHeight);
            graphics.Restore(state);
        }

        // Sprites face up by default.
        private static float GetTankRotationAngle(MoveDirection direction)
        {
            return direction switch
            {
                MoveDirection.Right => 90f,
                MoveDirection.Down => 180f,
                MoveDirection.Left => -90f,
                _ => 0f
            };
        }

        // Рисует номер уровня на корпусе танка
        private static void DrawTankLevelNumber(Graphics graphics, RectangleF bodyRect, Tank tank)
        {
            var text = ((int)tank.Level).ToString();
            var fontSize = Math.Max(8f, bodyRect.Height * 0.34f);

            using var font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            using var shadowBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            var shadowRect = new RectangleF(bodyRect.X + 1f, bodyRect.Y + 1f, bodyRect.Width, bodyRect.Height);
            graphics.DrawString(text, font, shadowBrush, shadowRect, format);
            graphics.DrawString(text, font, textBrush, bodyRect, format);
        }

        // Рисует полоску здоровья над танком
        private static void DrawTankHealthBar(Graphics graphics, float centerX, float topY, float bodySize, Tank tank)
        {
            if (tank.MaxHealth <= 0)
                return;

            var width = bodySize;
            var height = Math.Max(4f, bodySize * 0.08f);
            var x = centerX - width / 2f;
            var y = topY - height - 3f;
            var healthProgress = Math.Clamp((float)tank.Health / tank.MaxHealth, 0f, 1f);

            using var backgroundBrush = new SolidBrush(Color.FromArgb(110, 34, 34));
            using var healthBrush = new SolidBrush(Color.FromArgb(74, 214, 76));
            using var outlinePen = new Pen(Color.FromArgb(42, 42, 42), 1f);

            graphics.FillRectangle(backgroundBrush, x, y, width, height);
            graphics.FillRectangle(healthBrush, x, y, width * healthProgress, height);
            graphics.DrawRectangle(outlinePen, x, y, width, height);
        }

        // Рисует все активные снаряды
        private void DrawProjectiles(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            if (session.ActiveProjectiles.Count == 0)
                return;

            using var projectileBrush = new SolidBrush(Color.FromArgb(232, 120, 48));
            using var projectileOutlinePen = new Pen(Color.FromArgb(143, 63, 0), 1.5f);

            foreach (var projectile in session.ActiveProjectiles)
            {
                var projectileX = mapBounds.X + projectile.X * cellSize;
                var projectileY = mapBounds.Y + projectile.Y * cellSize;
                var projectileSize = Math.Max(8f, cellSize * 0.18f);

                graphics.FillEllipse(
                    projectileBrush,
                    projectileX - projectileSize / 2f,
                    projectileY - projectileSize / 2f,
                    projectileSize,
                    projectileSize
                );
                graphics.DrawEllipse(
                    projectileOutlinePen,
                    projectileX - projectileSize / 2f,
                    projectileY - projectileSize / 2f,
                    projectileSize,
                    projectileSize
                );
            }
        }

        // Рисует характеристики игрока слева от карты
        private void DrawPlayerStats(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            var tank = session.PlayerTank;
            var statLines = new[]
            {
                $"LVL: {(int)tank.Level}",
                $"HP: {tank.Health}/{tank.MaxHealth}",
                $"DMG: {tank.Damage}",
                $"DEF: {tank.Defense}",
                $"SPD: {tank.MoveSecondsPerCell:0.##}s"
            };

            using var font = new Font(FontFamily.GenericSansSerif, 11f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(52, 56, 48));

            var lineHeight = font.GetHeight(graphics) + 6f;
            var x = Math.Max(MapPadding, mapBounds.X - StatsMapGap - StatsPanelWidth);
            var y = mapBounds.Y + Math.Max(0f, cellSize * 0.25f);

            for (var i = 0; i < statLines.Length; i++)
                graphics.DrawString(statLines[i], font, textBrush, x, y + i * lineHeight);
        }

        // Считает размер и положение карты на экране
        private RectangleF GetMapBounds()
        {
            var reservedLeftWidth = StatsPanelWidth + StatsMapGap;
            var availableWidth = Math.Max(120f, Width - MapPadding * 2f - reservedLeftWidth);
            var availableHeight = Math.Max(120f, Height - MapPadding * 2f);
            var cellSize = Math.Min(availableWidth / GameMap.Width, availableHeight / GameMap.Height);
            var mapWidth = cellSize * GameMap.Width;
            var mapHeight = cellSize * GameMap.Height;

            return new RectangleF(
                reservedLeftWidth + (Width - reservedLeftWidth - mapWidth) / 2f,
                (Height - mapHeight) / 2f,
                mapWidth,
                mapHeight
            );
        }

        // Возвращает конец пушки танка
        private static PointF GetBarrelEndPoint(float centerX, float centerY, float cellSize, MoveDirection direction)
        {
            var barrelLength = cellSize * 0.46f;

            return direction switch
            {
                MoveDirection.Up => new PointF(centerX, centerY - barrelLength),
                MoveDirection.Down => new PointF(centerX, centerY + barrelLength),
                MoveDirection.Left => new PointF(centerX - barrelLength, centerY),
                MoveDirection.Right => new PointF(centerX + barrelLength, centerY),
                _ => new PointF(centerX, centerY)
            };
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                Focus();
        }

        private static Dictionary<TankLevel, Image> LoadTankSprites(string fileNamePrefix)
        {
            var sprites = new Dictionary<TankLevel, Image>();
            var assetsDirectory = FindTankAssetsDirectory();

            if (assetsDirectory == null)
                return sprites;

            LoadTankSprite(sprites, assetsDirectory, fileNamePrefix, TankLevel.Level1);
            LoadTankSprite(sprites, assetsDirectory, fileNamePrefix, TankLevel.Level2);
            LoadTankSprite(sprites, assetsDirectory, fileNamePrefix, TankLevel.Level3);

            return sprites;
        }

        private static Dictionary<TerrainType, Image> LoadTerrainSprites()
        {
            var sprites = new Dictionary<TerrainType, Image>();
            var assetsDirectory = FindNatureAssetsDirectory();

            if (assetsDirectory == null)
                return sprites;

            LoadTerrainSprite(sprites, assetsDirectory, TerrainType.Empty, "dirt");
            LoadTerrainSprite(sprites, assetsDirectory, TerrainType.Bush, "bush");
            LoadTerrainSprite(sprites, assetsDirectory, TerrainType.Stone, "stone");
            LoadTerrainSprite(sprites, assetsDirectory, TerrainType.Water, "water");

            return sprites;
        }

        private static void LoadTerrainSprite(
            IDictionary<TerrainType, Image> sprites,
            string assetsDirectory,
            TerrainType terrain,
            string fileName
        )
        {
            var filePath = FindSpritePath(assetsDirectory, fileName);

            if (filePath == null)
                return;

            using var loadedImage = Image.FromFile(filePath);
            sprites[terrain] = new Bitmap(loadedImage);
        }

        private static void LoadTankSprite(
            IDictionary<TankLevel, Image> sprites,
            string assetsDirectory,
            string fileNamePrefix,
            TankLevel level
        )
        {
            var filePath = FindTankSpritePath(assetsDirectory, fileNamePrefix, level);

            if (filePath == null)
                return;

            using var loadedImage = Image.FromFile(filePath);
            sprites[level] = new Bitmap(loadedImage);
        }

        private static string? FindTankSpritePath(string assetsDirectory, string fileNamePrefix, TankLevel level)
        {
            var baseName = $"{fileNamePrefix}-{(int)level}";
            return FindSpritePath(assetsDirectory, baseName);
        }

        private static string? FindSpritePath(string assetsDirectory, string baseName)
        {
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                var exactPath = Path.Combine(assetsDirectory, baseName + extension);

                if (File.Exists(exactPath))
                    return exactPath;
            }

            foreach (var extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                var candidates = Directory.GetFiles(assetsDirectory, baseName + "*" + extension);
                Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);

                if (candidates.Length > 0)
                    return candidates[0];
            }

            return null;
        }

        private static string? FindTankAssetsDirectory()
        {
            return FindAssetDirectory(Path.Combine("assets", "parts_of_tanks", "bodies")) ??
                FindAssetDirectory(Path.Combine("assets", "parts_of_tanks", "weapons"));
        }

        private static string? FindNatureAssetsDirectory()
        {
            return FindAssetDirectory(Path.Combine("assets", "nature_selection"));
        }

        private static string? FindAssetDirectory(string relativeAssetsDirectory)
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

            while (currentDirectory != null)
            {
                var candidate = Path.Combine(currentDirectory.FullName, relativeAssetsDirectory);

                if (Directory.Exists(candidate))
                    return candidate;

                currentDirectory = currentDirectory.Parent;
            }

            var workingDirectoryCandidate = Path.Combine(Environment.CurrentDirectory, relativeAssetsDirectory);
            return Directory.Exists(workingDirectoryCandidate) ? workingDirectoryCandidate : null;
        }

        private static void DisposeImages(IEnumerable<Image> images)
        {
            foreach (var image in images)
                image.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                DisposeImages(playerTankSprites.Values);
                DisposeImages(enemyTankSprites.Values);
                DisposeImages(terrainSprites.Values);
                gameOverImage?.Dispose();
                audioService.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
