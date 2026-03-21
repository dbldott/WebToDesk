using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebToDesk;

public partial class Form1 : Form
{
    private readonly HttpClient _httpClient = new();

    private string _sessionId = string.Empty;
    private bool _isWordOpen = false;

    private DateTimeOffset? _startedAt = null;
    private DateTimeOffset? _closedAt = null;

    private SynchronizationContext? _uiContext;

    private const string ApiBaseUrl = "http://localhost:5000";

    public Form1()
    {
        InitializeComponent();

        this.Shown += Form1_Shown;
        notifyIcon1.DoubleClick += NotifyIcon1_DoubleClick;
    }

    private void Form1_Shown(object? sender, EventArgs e)
    {
        _uiContext = SynchronizationContext.Current;
        HideToTray();
        AppCommands.Register(OpenWordFromSignal);
    }

    private void OpenWordFromSignal(string sessionId)
    {
        _uiContext?.Post(_ => _ = StartWordSessionAsync(sessionId), null);
    }

    private async Task StartWordSessionAsync(string sessionId)
    {
        if (_isWordOpen) return;

        _sessionId = sessionId;

        try
        {
            var wordProcess = StartWord();

            if (wordProcess == null)
            {
                await SendStatusAsync("error", "Не удалось запустить Word.");
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
            // Приложение остаётся в трее — Word закрылся, но мы продолжаем работать
        }
        catch (Exception ex)
        {
            _isWordOpen = false;
            _closedAt = DateTimeOffset.Now;

            await SendStatusAsync("error", ex.Message);
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