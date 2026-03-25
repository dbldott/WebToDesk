using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors();

        var app = builder.Build();
        app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        var statusStore = new ConcurrentDictionary<string, WordStatusUpdateRequest>();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Открыть Word
        app.MapPost("/open-app", (OpenAppRequest request) =>
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString()
                : request.SessionId;

            AppCommands.RequestOpenWord(sessionId);

            return Results.Ok(new { message = "Сигнал получен", sessionId });
        });

        // Статус Word
        app.MapPost("/word-status-update", (WordStatusUpdateRequest dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.SessionId))
                return Results.BadRequest("SessionId обязателен");

            statusStore[dto.SessionId] = dto;
            return Results.Ok(new { message = "Статус получен" });
        });

        app.MapGet("/word-status/{sessionId}", (string sessionId) =>
        {
            if (!statusStore.TryGetValue(sessionId, out var status))
                return Results.NotFound("Статус не найден");
            return Results.Ok(status);
        });

        // ── Чтение DOCX из MinIO → возвращает plain text ──
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
                    {
                        sb.AppendLine(para.InnerText);
                    }
                }

                return Results.Ok(new { text = sb.ToString() });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка чтения DOCX: {ex.Message}");
            }
        });

        // ── Сохранение текста обратно в DOCX и загрузка в MinIO ──
        app.MapPost("/docx-save", async (DocxSaveRequest req) =>
        {
            try
            {
                // Скачиваем оригинальный файл из MinIO
                using var httpClient = new HttpClient();
                var originalBytes = await httpClient.GetByteArrayAsync(req.Url);

                // Открываем и меняем текст
                using var ms = new MemoryStream();
                ms.Write(originalBytes, 0, originalBytes.Length);
                ms.Position = 0;

                using (var doc = WordprocessingDocument.Open(ms, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        // Удаляем все параграфы
                        body.RemoveAllChildren<Paragraph>();

                        // Вставляем новые параграфы из текста
                        var lines = req.Text.Split('\n');
                        foreach (var line in lines)
                        {
                            var para = new Paragraph(new Run(new Text(line.TrimEnd('\r'))));
                            body.AppendChild(para);
                        }

                        doc.MainDocumentPart!.Document.Save();
                    }
                }

                // Загружаем обновлённый файл обратно в MinIO
                var content = new ByteArrayContent(ms.ToArray());
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

                var response = await httpClient.PutAsync(req.Url, content);

                if (response.IsSuccessStatusCode)
                    return Results.Ok(new { message = "Сохранено" });
                else
                    return Results.Problem($"Ошибка загрузки в MinIO: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Ошибка сохранения DOCX: {ex.Message}");
            }
        });

        app.MapGet("/", () => Results.Redirect("/index.html"));
        app.MapFallbackToFile("index.html");

        Task.Run(() => app.Run("http://localhost:5000"));

        Application.Run(new Form1());
    }
}

public class OpenAppRequest
{
    public string SessionId { get; set; } = string.Empty;
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

    public static void Register(Action<string> handler) => _openWord = handler;
    public static void RequestOpenWord(string sessionId) => _openWord?.Invoke(sessionId);
}
