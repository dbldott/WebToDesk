using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;

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

        // Хранилище статусов Word в памяти
        var statusStore = new ConcurrentDictionary<string, WordStatusUpdateRequest>();

        // Раздача встроенной вебки из wwwroot
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Кнопка "Открыть Word" из веб-UI — даёт сигнал десктопу открыть Word
        app.MapPost("/open-app", (OpenAppRequest request) =>
        {
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
                ? Guid.NewGuid().ToString()
                : request.SessionId;

            AppCommands.RequestOpenWord(sessionId);

            return Results.Ok(new
            {
                message = "Сигнал получен",
                sessionId
            });
        });

        app.MapPost("/word-status-update", (WordStatusUpdateRequest dto) =>
        {
            if (string.IsNullOrWhiteSpace(dto.SessionId))
                return Results.BadRequest("SessionId обязателен");

            statusStore[dto.SessionId] = dto;

            Console.WriteLine("----- WORD STATUS UPDATE -----");
            Console.WriteLine($"SessionId: {dto.SessionId}");
            Console.WriteLine($"Status: {dto.Status}");
            Console.WriteLine($"IsWordOpen: {dto.IsWordOpen}");
            Console.WriteLine($"StartedAt: {dto.StartedAt:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine($"ClosedAt: {dto.ClosedAt:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine($"ErrorMessage: {dto.ErrorMessage}");
            Console.WriteLine("------------------------------");

            return Results.Ok(new { message = "Статус получен" });
        });

        app.MapGet("/word-status/{sessionId}", (string sessionId) =>
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return Results.BadRequest("SessionId обязателен");

            if (!statusStore.TryGetValue(sessionId, out var status))
                return Results.NotFound("Статус не найден");

            return Results.Ok(status);
        });

        app.MapGet("/", () => Results.Redirect("/index.html"));

        app.MapFallbackToFile("index.html");

        // Поднимаем встроенный сервер в фоне
        Task.Run(() => app.Run("http://localhost:5000"));

        // Запускаем desktop-часть
        Application.Run(new Form1());
    }
}


public class OpenAppRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public static class AppCommands
{
    private static Action<string>? _openWord;

    public static void Register(Action<string> handler)
    {
        _openWord = handler;
    }

    public static void RequestOpenWord(string sessionId)
    {
        _openWord?.Invoke(sessionId);
    }
}