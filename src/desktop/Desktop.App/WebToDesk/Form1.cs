using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace WebToDesk;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();

        this.Shown += async (_, __) => await OpenWordThenExitAsync();

        // Через 5 секунд прячем окно
        timer1.Tick += Timer1_Tick;
        timer1.Start();

        // Двойной клик по иконке в трее — вернуть окно
        notifyIcon1.DoubleClick += NotifyIcon1_DoubleClick;
    }

    private void Timer1_Tick(object? sender, EventArgs e)
    {
        timer1.Stop();
        this.Hide();
        this.ShowInTaskbar = false;
    }

    private void NotifyIcon1_DoubleClick(object? sender, EventArgs e)
    {
        this.Show();
        this.ShowInTaskbar = true;
        this.WindowState = FormWindowState.Normal;
        this.Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Нажал крестик — не закрываем процесс, а прячем в трей
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
            this.ShowInTaskbar = false;
            return;
        }

        base.OnFormClosing(e);
    }

    private void SettingsItem_Click(object? sender, EventArgs e)
    {
        this.Show();
        this.ShowInTaskbar = true;
        this.WindowState = FormWindowState.Normal;
        this.Activate();
    }

    private void ExitItem_Click(object? sender, EventArgs e)
    {
        notifyIcon1.Visible = false;
        notifyIcon1.Dispose();
        Application.Exit();
    }

    private async Task OpenWordThenExitAsync()
{
    // 1) показать окно 5 сек
    await Task.Delay(5000);

    // 2) спрятаться в трей
    this.Hide();
    this.ShowInTaskbar = false;

    // 3) открыть Word
    var psi = new ProcessStartInfo
    {
        FileName = "winword.exe",
        UseShellExecute = true
    };

    using var p = Process.Start(psi);
    if (p == null)
    {
        MessageBox.Show("Не удалось запустить Word (winword.exe).");
        return;
    }

    // 4) ждать закрытия Word
    await p.WaitForExitAsync();

    // 5) закрыть наше приложение
    ExitApp();
}

private void ExitApp()
{
    notifyIcon1.Visible = false;
    notifyIcon1.Dispose();
    Application.Exit();
}
}