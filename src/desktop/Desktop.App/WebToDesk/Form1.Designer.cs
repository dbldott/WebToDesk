namespace WebToDesk;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.NotifyIcon notifyIcon1;
    private System.Windows.Forms.Timer timer1;
    private System.Windows.Forms.ContextMenuStrip trayMenu;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        notifyIcon1 = new System.Windows.Forms.NotifyIcon(components);
        timer1 = new System.Windows.Forms.Timer(components);
        trayMenu = new System.Windows.Forms.ContextMenuStrip(components);
        // пункты меню
        var settingsItem = new ToolStripMenuItem("Настройки");
        settingsItem.Click += SettingsItem_Click;

        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += ExitItem_Click;

        trayMenu.Items.Add(settingsItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(exitItem);

        notifyIcon1.ContextMenuStrip = trayMenu;

        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 450);
        Text = "WebToDesk";

        // Трей-иконка
        notifyIcon1.Text = "WebToDesk";
        notifyIcon1.Visible = true;

        // Иконка (лучше через BaseDirectory, чтобы файл точно находился)
        notifyIcon1.Icon = new System.Drawing.Icon(
            System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico")
        );

        // Таймер на 5 секунд
        timer1.Interval = 2000;
    }
}