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
            ShowScreen(new MainMenuScreen(this));
        }

        public void ShowGame()
        {
            var screen = new GameScreen(this);
            ShowScreen(screen);
            screen.Focus();
        }

        public void ShowMapEditor()
        {
            ShowScreen(new MapEditorScreen(this));
        }

        private void ShowScreen(UserControl screen)
        {
            SuspendLayout();

            while (Controls.Count > 0)
            {
                var control = Controls[0];
                Controls.RemoveAt(0);
                control.Dispose();
            }

            screen.Dock = DockStyle.Fill;
            Controls.Add(screen);
            ResumeLayout();
        }
    }
}
