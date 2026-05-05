using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyGame.Screens
{
    public class MainMenuScreen : UserControl
    {
        private readonly MainForm mainForm;

        public MainMenuScreen(MainForm mainForm)
        {
            this.mainForm = mainForm;
            InitializeUi();
        }

        private void InitializeUi()
        {
            BackColor = Color.White;

            var playButton = new Button
            {
                Text = "Играть",
                Size = new Size(160, 50)
            };

            var exitButton = new Button
            {
                Text = "Выйти",
                Size = new Size(160, 50)
            };

            var settingsButton = new Button
            {
                Text = "Настройки",
                Size = new Size(120, 40),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            playButton.Click += (_, _) => mainForm.ShowGame();
            exitButton.Click += (_, _) => mainForm.Close();
            settingsButton.Click += (_, _) => mainForm.ShowSettings();

            Controls.Add(playButton);
            Controls.Add(exitButton);
            Controls.Add(settingsButton);

            Resize += (_, _) =>
            {
                playButton.Location = new Point(
                    (Width - playButton.Width) / 2,
                    Height / 2 - 60
                );

                exitButton.Location = new Point(
                    (Width - exitButton.Width) / 2,
                    Height / 2 + 10
                );

                settingsButton.Location = new Point(
                    Width - settingsButton.Width - 20,
                    Height - settingsButton.Height - 20
                );
            };
        }

        private void InitializeComponent()
        {

        }
    }
}