using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyGame.Screens
{
    public class SettingsScreen : UserControl
    {
        private readonly MainForm mainForm;

        public SettingsScreen(MainForm mainForm)
        {
            this.mainForm = mainForm;
            BackColor = Color.White;

            var backButton = new Button
            {
                Text = "Назад",
                Size = new Size(120, 40),
                Location = new Point(20, 20)
            };

            backButton.Click += (_, _) => mainForm.ShowMainMenu();
            Controls.Add(backButton);
        }
    }
}