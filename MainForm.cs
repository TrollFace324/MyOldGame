using System;
using System.Windows.Forms;
using MyGame.Screens;

namespace MyGame
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            ShowMainMenu();
        }

        public void ShowMainMenu()
        {
            Controls.Clear();
            var screen = new MainMenuScreen(this);
            screen.Dock = DockStyle.Fill;
            Controls.Add(screen);
        }

        public void ShowGame()
        {
            Controls.Clear();
            var screen = new GameScreen(this);
            screen.Dock = DockStyle.Fill;
            Controls.Add(screen);
            screen.Focus();
        }

        public void ShowSettings()
        {
            Controls.Clear();
            var screen = new SettingsScreen(this);
            screen.Dock = DockStyle.Fill;
            Controls.Add(screen);
        }
    }
}
