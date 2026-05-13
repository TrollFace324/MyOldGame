using System;
using System.Windows.Forms;

namespace MyGame
{
    internal static class Program
    {
        [STAThread]
        // Запускает WinForms приложение
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
