using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WebToDesk;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
        });
        builder.Services.AddCors();

        var app = builder.Build();
        app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        var statusStore = new ConcurrentDictionary<string, WordStatusUpdateRequest>();
        WordStatusUpdateRequest? latestWordStatus = null;

        app.UseDefaultFiles();
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypeProvider
        });

        // ── Открыть Word (без файла) ──
        app.MapPost("/open-app", (OpenAppRequest request) =>
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString()
                : request.SessionId;

            AppCommands.RequestOpenWord(sessionId);
            return Results.Ok(new { message = "Сигнал получен", sessionId });
        });

        // ── Открыть конкретный файл из MinIO в Word ──
        app.MapPost("/open-file-in-word", async (OpenFileInWordRequest request) =>
        {
            try
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(request.MinioUrl);

                var tempDir = Path.Combine(Path.GetTempPath(), "WebToDesk");
                Directory.CreateDirectory(tempDir);
                var localPath = Path.Combine(tempDir, request.FileName);

                await File.WriteAllBytesAsync(localPath, bytes);

                AppCommands.RequestOpenFileInWord(new OpenFileSession
                {
                    SessionId = request.SessionId,
                    LocalPath = localPath,
                    MinioUrl = request.MinioUrl,
                    FileName = request.FileName
                });

                return Results.Ok(new { message = "Открываем в Word", localPath });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка: {ex.Message}");
            }
        });

        // ── Статус Word по sessionId ──
        app.MapPost("/word-status-update", (WordStatusUpdateRequest dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.SessionId))
                return Results.BadRequest("SessionId обязателен");

            statusStore[dto.SessionId] = dto;
            latestWordStatus = dto;

            return Results.Ok(new { message = "Статус получен" });
        });

        app.MapGet("/word-status/{sessionId}", (string sessionId) =>
        {
            if (!statusStore.TryGetValue(sessionId, out var status))
                return Results.NotFound("Статус не найден");

            return Results.Ok(status);
        });

        // ── Глобальный статус Word для веб-оверлея ──
        app.MapGet("/word-active", () =>
        {
            if (latestWordStatus is null)
            {
                return Results.Ok(new
                {
                    isWordOpen = false,
                    status = "idle",
                    sessionId = (string?)null,
                    startedAt = (DateTimeOffset?)null,
                    closedAt = (DateTimeOffset?)null,
                    errorMessage = (string?)null
                });
            }

            return Results.Ok(new
            {
                isWordOpen = latestWordStatus.IsWordOpen,
                status = latestWordStatus.Status,
                sessionId = latestWordStatus.SessionId,
                startedAt = latestWordStatus.StartedAt,
                closedAt = latestWordStatus.ClosedAt,
                errorMessage = latestWordStatus.ErrorMessage
            });
        });

        // ── Чтение DOCX → plain text (для браузерного редактора) ──
        app.MapGet("/docx-read", async (string url) =>
        {
            try
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(url);

                using var ms = new MemoryStream(bytes);
                using var doc = WordprocessingDocument.Open(ms, false);

                var sb = new StringBuilder();
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    foreach (var para in body.Elements<Paragraph>())
                        sb.AppendLine(para.InnerText);
                }

                return Results.Ok(new { text = sb.ToString() });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка чтения DOCX: {ex.Message}");
            }
        });

        // ── Сохранение текста → DOCX → MinIO (для браузерного редактора) ──
        app.MapPost("/docx-save", async (DocxSaveRequest req) =>
        {
            try
            {
                using var httpClient = new HttpClient();
                var originalBytes = await httpClient.GetByteArrayAsync(req.Url);

                using var ms = new MemoryStream();
                ms.Write(originalBytes, 0, originalBytes.Length);
                ms.Position = 0;

                using (var doc = WordprocessingDocument.Open(ms, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        body.RemoveAllChildren<Paragraph>();

                        foreach (var line in req.Text.Split('\n'))
                        {
                            var para = new Paragraph(new Run(new Text(line.TrimEnd('\r'))));
                            body.AppendChild(para);
                        }

                        doc.MainDocumentPart!.Document.Save();
                    }
                }

                var content = new ByteArrayContent(ms.ToArray());
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

                var response = await httpClient.PutAsync(req.Url, content);

                return response.IsSuccessStatusCode
                    ? Results.Ok(new { message = "Сохранено" })
                    : Results.Problem($"Ошибка MinIO: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка: {ex.Message}");
            }
        });

        app.MapGet("/", () => Results.Redirect("/index.html"));
        app.MapFallbackToFile("index.html");

        Task.Run(() => app.Run("http://localhost:5000"));

        Application.Run(new Form1());
    }
}

// ── Модели ──
public class OpenAppRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public class OpenFileInWordRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MinioUrl { get; set; } = string.Empty;
}

public class OpenFileSession
{
    public string SessionId { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string MinioUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
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

public class DocxSaveRequest
{
    public string Url { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public static class AppCommands
{
    private static Action<string>? _openWord;
    private static Action<OpenFileSession>? _openFileInWord;

    public static void Register(Action<string> handler) => _openWord = handler;
    public static void RegisterOpenFile(Action<OpenFileSession> handler) => _openFileInWord = handler;

    public static void RequestOpenWord(string sessionId) => _openWord?.Invoke(sessionId);
    public static void RequestOpenFileInWord(OpenFileSession session) => _openFileInWord?.Invoke(session);
}