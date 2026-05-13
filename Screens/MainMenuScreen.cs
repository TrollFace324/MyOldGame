using System;
using System.Drawing;
using System.Windows.Forms;
using MyGame.Services;

namespace MyGame.Screens
{
    public class MainMenuScreen : UserControl
    {
        private readonly MainForm mainForm;
        private readonly Image? backgroundImage;
        private readonly Control[] menuButtons;

        public MainMenuScreen(MainForm mainForm)
        {
            this.mainForm = mainForm;
            BackColor = Color.Black;
            DoubleBuffered = true;

            backgroundImage = ImageAssetService.LoadImage("assets", "background", "main-menu.png");

            var playButton = CreateImageButton(
                ImageAssetService.LoadImage("assets", "buttons", "start.png"),
                "Играть");
            var mapEditorButton = CreateImageButton(
                ImageAssetService.LoadImage("assets", "buttons", "options.png"),
                "Редактор карты");
            var exitButton = CreateImageButton(
                ImageAssetService.LoadImage("assets", "buttons", "exit.png"),
                "Выйти");

            menuButtons = [playButton, mapEditorButton, exitButton];

            playButton.Click += (_, _) => this.mainForm.ShowGame();
            mapEditorButton.Click += (_, _) => this.mainForm.ShowMapEditor();
            exitButton.Click += (_, _) => this.mainForm.Close();

            Controls.AddRange(menuButtons);
            Resize += (_, _) => LayoutMenuButtons();
            LayoutMenuButtons();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (backgroundImage == null)
            {
                e.Graphics.Clear(Color.Black);
                return;
            }

            DrawCoverImage(e.Graphics, backgroundImage, ClientRectangle);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                backgroundImage?.Dispose();

                foreach (var button in menuButtons)
                {
                    if (button is PictureBox pictureButton)
                        pictureButton.Image?.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void LayoutMenuButtons()
        {
            if (Width <= 0 || Height <= 0)
                return;

            var gap = Math.Max(12, Height / 48);
            var y = (int)(Height * 0.53f);

            foreach (var button in menuButtons)
            {
                button.Size = GetMenuButtonSize(button);
                button.Location = new Point((Width - button.Width) / 2, y);
                y += button.Height + gap;
            }
        }

        private Size GetMenuButtonSize(Control button)
        {
            var maxWidth = Math.Max(160, Math.Min(360, Width - 80));
            var targetHeight = Math.Max(48, Math.Min(92, Height / 9));

            if (button is PictureBox { Image: { } image })
            {
                var aspect = (float)image.Width / image.Height;
                var width = Math.Min(maxWidth, (int)(targetHeight * aspect));
                var height = Math.Max(42, (int)(width / aspect));
                return new Size(width, height);
            }

            return new Size(Math.Min(maxWidth, 220), targetHeight);
        }

        private static Control CreateImageButton(Image? image, string fallbackText)
        {
            if (image == null)
            {
                return new Button
                {
                    Text = fallbackText,
                    Cursor = Cursors.Hand,
                    UseVisualStyleBackColor = true
                };
            }

            return new PictureBox
            {
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom
            };
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
    }
}
