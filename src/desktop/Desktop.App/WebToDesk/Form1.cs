using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebToDesk;

public partial class Form1 : Form
{
    private readonly HttpClient _httpClient = new();

    // Больше не генерируем sessionId здесь.
    // Теперь он приходит снаружи, из Program.cs.
    private readonly string _sessionId;

    private bool _isWordOpen = false;

    private DateTimeOffset? _startedAt = null;
    private DateTimeOffset? _closedAt = null;

    private const string ApiBaseUrl = "http://localhost:5298";
    private const int StartupDelayMs = 2000;

    // Конструктор теперь принимает sessionId
    public Form1(string sessionId)
    {
        InitializeComponent();

        _sessionId = sessionId;

        this.Shown += async (_, __) => await StartWorkflowAsync();
        notifyIcon1.DoubleClick += NotifyIcon1_DoubleClick;
    }

    private async Task StartWorkflowAsync()
    {
        try
        {
            await Task.Delay(StartupDelayMs);

            HideToTray();

            var wordProcess = StartWord();

            if (wordProcess == null)
            {
                await SendStatusAsync("error", "Не удалось запустить Word.");
                MessageBox.Show("Не удалось запустить Word (winword.exe).");
                return;
            }

            _isWordOpen = true;
            _startedAt = DateTimeOffset.Now;
            _closedAt = null;

            await SendStatusAsync("running");

            await wordProcess.WaitForExitAsync();

            _isWordOpen = false;
            _closedAt = DateTimeOffset.Now;

            await SendStatusAsync("closed");

            ExitApp();
        }
        catch (Exception ex)
        {
            _isWordOpen = false;
            _closedAt = DateTimeOffset.Now;

            await SendStatusAsync("error", ex.Message);
            MessageBox.Show($"Ошибка: {ex.Message}");
        }
    }

    private Process? StartWord()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "winword.exe",
            UseShellExecute = true
        };

        return Process.Start(psi);
    }

    private void HideToTray()
    {
        this.Hide();
        this.ShowInTaskbar = false;
    }

    private void ShowFromTray()
    {
        this.Show();
        this.ShowInTaskbar = true;
        this.WindowState = FormWindowState.Normal;
        this.Activate();
    }

    private void NotifyIcon1_DoubleClick(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    private void SettingsItem_Click(object? sender, EventArgs e)
    {
        ShowFromTray();
    }

    private void ExitItem_Click(object? sender, EventArgs e)
    {
        ExitApp();
    }

    private void ExitApp()
    {
        notifyIcon1.Visible = false;
        notifyIcon1.Dispose();
        Application.Exit();
    }

    private async Task SendStatusAsync(string status, string? errorMessage = null)
    {
        try
        {
            var payload = new WordStatusUpdateRequest
            {
                SessionId = _sessionId,
                Status = status,
                IsWordOpen = _isWordOpen,
                StartedAt = _startedAt,
                ClosedAt = _closedAt,
                ErrorMessage = errorMessage
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{ApiBaseUrl}/word-status-update",
                payload
            );

            response.EnsureSuccessStatusCode();
        }
        catch
        {
        }
    }
}

public class WordStatusUpdateRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsWordOpen { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ErrorMessage { get; set; }
}