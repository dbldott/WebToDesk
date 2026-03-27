using System;
using System.Diagnostics;
using System.IO;
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

    // FileSystemWatcher для отслеживания изменений открытого файла
    private FileSystemWatcher? _watcher;
    private string? _watchedLocalPath;
    private string? _watchedMinioUrl;
    private DateTime _lastUploadTime = DateTime.MinValue;

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
        AppCommands.RegisterOpenFile(OpenFileInWordFromSignal);
    }

    // ── Старая логика: просто открыть Word ──
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

    // ── Новая логика: открыть конкретный файл из MinIO в Word ──
    private void OpenFileInWordFromSignal(OpenFileSession session)
    {
        _uiContext?.Post(_ => _ = StartFileWordSessionAsync(session), null);
    }

    private async Task StartFileWordSessionAsync(OpenFileSession session)
    {
        try
        {
            _sessionId = session.SessionId;
            _watchedLocalPath = session.LocalPath;
            _watchedMinioUrl = session.MinioUrl;

            // Запускаем FileSystemWatcher — следим за изменениями файла
            StartWatcher(session.LocalPath, session.MinioUrl);

            // Открываем файл в Word
            var psi = new ProcessStartInfo
            {
                FileName = "winword.exe",
                Arguments = $"\"{session.LocalPath}\"",
                UseShellExecute = true
            };

            var wordProcess = Process.Start(psi);

            if (wordProcess == null)
            {
                await SendStatusAsync("error", "Не удалось запустить Word.");
                StopWatcher();
                return;
            }

            _isWordOpen = true;
            _startedAt = DateTimeOffset.Now;
            _closedAt = null;
            await SendStatusAsync("running");

            // Ждём закрытия Word
            await wordProcess.WaitForExitAsync();

            _isWordOpen = false;
            _closedAt = DateTimeOffset.Now;

            // Финальная загрузка файла в MinIO после закрытия Word
            await UploadFileToMinioAsync(session.LocalPath, session.MinioUrl);

            StopWatcher();

            await SendStatusAsync("closed");

            // Удаляем временный файл
            try { File.Delete(session.LocalPath); } catch { }
        }
        catch (Exception ex)
        {
            _isWordOpen = false;
            _closedAt = DateTimeOffset.Now;
            StopWatcher();
            await SendStatusAsync("error", ex.Message);
        }
    }

    // ── FileSystemWatcher: следим за сохранением файла в Word ──
    private void StartWatcher(string localPath, string minioUrl)
    {
        StopWatcher();

        var dir = Path.GetDirectoryName(localPath)!;
        var fileName = Path.GetFileName(localPath);

        _watcher = new FileSystemWatcher(dir, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += async (s, e) =>
        {
            // Дебаунс — не чаще раза в 3 секунды (Word сохраняет несколько событий подряд)
            if ((DateTime.Now - _lastUploadTime).TotalSeconds < 3) return;
            _lastUploadTime = DateTime.Now;

            // Небольшая пауза — Word ещё может писать файл
            await Task.Delay(1000);
            await UploadFileToMinioAsync(localPath, minioUrl);
        };
    }

    private void StopWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    // ── Загрузка файла в MinIO ──
    private async Task UploadFileToMinioAsync(string localPath, string minioUrl)
    {
        try
        {
            // Читаем файл с retry — Word может держать блокировку
            byte[] bytes = Array.Empty<byte>();
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    bytes = await File.ReadAllBytesAsync(localPath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(500);
                }
            }

            if (bytes.Length == 0) return;

            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

            await _httpClient.PutAsync(minioUrl, content);
        }
        catch
        {
            // Тихо игнорируем — не прерываем работу пользователя
        }
    }

    // ── Стандартные методы формы ──
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

    private void NotifyIcon1_DoubleClick(object? sender, EventArgs e) => ShowFromTray();

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

    private void SettingsItem_Click(object? sender, EventArgs e) => ShowFromTray();
    private void ExitItem_Click(object? sender, EventArgs e) => ExitApp();

    private void ExitApp()
    {
        StopWatcher();
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

            await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}/word-status-update", payload);
        }
        catch { }
    }
}
