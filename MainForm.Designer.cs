namespace MyGame
{
    partial class MainForm
    {
        // Контейнер компонентов формы
        private System.ComponentModel.IContainer components = null;

        // Освобождает ресурсы формы
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        // Настраивает форму из дизайнерского файла
        private void InitializeComponent()
        {
            float sizeKoof = 2.3f;
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size((int)(960 * sizeKoof), (int)(720 * sizeKoof));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Text = "MyOldGame";
        }

        #endregion
    }
}
