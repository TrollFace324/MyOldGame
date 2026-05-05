using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private DateTime lastUpdateTime;
        private const float MapPadding = 32f;

        public GameScreen(MainForm mainForm)
        {
            this.mainForm = mainForm;
            BackColor = Color.White;
            DoubleBuffered = true;
            TabStop = true;

            session = new GameSession(new RandomSpawnService());

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += OnTick;
            lastUpdateTime = DateTime.Now;
            timer.Start();

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var deltaTime = (float)(now - lastUpdateTime).TotalSeconds;
            lastUpdateTime = now;

            session.Update(deltaTime);
            Invalidate();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                case Keys.Up:
                    session.SetDirection(MoveDirection.Up);
                    break;
                case Keys.S:
                case Keys.Down:
                    session.SetDirection(MoveDirection.Down);
                    break;
                case Keys.A:
                case Keys.Left:
                    session.SetDirection(MoveDirection.Left);
                    break;
                case Keys.D:
                case Keys.Right:
                    session.SetDirection(MoveDirection.Right);
                    break;
                case Keys.Space:
                    session.Fire();
                    break;
                case Keys.Escape:
                    timer.Stop();
                    mainForm.ShowMainMenu();
                    break;
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                case Keys.Up:
                case Keys.S:
                case Keys.Down:
                case Keys.A:
                case Keys.Left:
                case Keys.D:
                case Keys.Right:
                    session.SetDirection(MoveDirection.None);
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var mapBounds = GetMapBounds();
            var cellSize = mapBounds.Width / GameMap.Width;

            DrawMap(e.Graphics, mapBounds, cellSize);
            DrawTank(e.Graphics, mapBounds, cellSize);
            DrawProjectiles(e.Graphics, mapBounds, cellSize);
        }

        private void DrawMap(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            using var mapBrush = new SolidBrush(Color.FromArgb(244, 238, 214));
            using var cellPen = new Pen(Color.FromArgb(198, 187, 145), 1f);
            using var borderPen = new Pen(Color.FromArgb(110, 88, 56), 3f);

            graphics.FillRectangle(mapBrush, mapBounds);

            for (var row = 0; row < GameMap.Height; row++)
            {
                for (var column = 0; column < GameMap.Width; column++)
                {
                    var cellX = mapBounds.X + column * cellSize;
                    var cellY = mapBounds.Y + row * cellSize;
                    graphics.DrawRectangle(cellPen, cellX, cellY, cellSize, cellSize);
                }
            }

            graphics.DrawRectangle(borderPen, mapBounds.X, mapBounds.Y, mapBounds.Width, mapBounds.Height);
        }

        private void DrawTank(Graphics graphics, RectangleF mapBounds, float cellSize)
        {
            var tankCell = session.PlayerTank.CurrentCell;
            var cellX = mapBounds.X + tankCell.Column * cellSize;
            var cellY = mapBounds.Y + tankCell.Row * cellSize;
            var bodySize = cellSize * 0.72f;
            var centerX = cellX + cellSize / 2f;
            var centerY = cellY + cellSize / 2f;

            var bodyRect = new RectangleF(
                cellX + (cellSize - bodySize) / 2f,
                cellY + (cellSize - bodySize) / 2f,
                bodySize,
                bodySize);

            var barrelEnd = GetBarrelEndPoint(centerX, centerY, cellSize, session.PlayerTank.FacingDirection);

            using var barrelPen = new Pen(Color.FromArgb(80, 80, 80), Math.Max(4f, cellSize * 0.16f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            using var bodyBrush = new SolidBrush(Color.FromArgb(48, 120, 84));
            using var bodyOutlinePen = new Pen(Color.FromArgb(28, 64, 46), 2f);
            using var hatchBrush = new SolidBrush(Color.FromArgb(218, 206, 154));

            graphics.DrawLine(barrelPen, centerX, centerY, barrelEnd.X, barrelEnd.Y);
            graphics.FillRectangle(bodyBrush, bodyRect);
            graphics.DrawRectangle(bodyOutlinePen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);

            var hatchSize = bodySize * 0.24f;
            graphics.FillEllipse(
                hatchBrush,
                centerX - hatchSize / 2f,
                centerY - hatchSize / 2f,
                hatchSize,
                hatchSize);
        }

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
                    projectileSize);
                graphics.DrawEllipse(
                    projectileOutlinePen,
                    projectileX - projectileSize / 2f,
                    projectileY - projectileSize / 2f,
                    projectileSize,
                    projectileSize);
            }
        }

        private RectangleF GetMapBounds()
        {
            var availableWidth = Math.Max(120f, Width - MapPadding * 2f);
            var availableHeight = Math.Max(120f, Height - MapPadding * 2f);
            var cellSize = Math.Min(availableWidth / GameMap.Width, availableHeight / GameMap.Height);
            var mapWidth = cellSize * GameMap.Width;
            var mapHeight = cellSize * GameMap.Height;

            return new RectangleF(
                (Width - mapWidth) / 2f,
                (Height - mapHeight) / 2f,
                mapWidth,
                mapHeight);
        }

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
    }
}
