using System.Diagnostics;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var app = builder.Build();

app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

var statusStore = new ConcurrentDictionary<string, WordStatusUpdateRequest>();

app.MapPost("/open-app", (OpenAppRequest request) =>
{
    var baseDir = AppContext.BaseDirectory;
    var exePath = Path.GetFullPath(Path.Combine(baseDir, "..", "Desktop", "WebToDesk.exe"));
    var exeDir = Path.GetDirectoryName(exePath)!;

    Console.WriteLine("===== OPEN-APP =====");
    Console.WriteLine($"SessionId: {request.SessionId}");
    Console.WriteLine($"Api BaseDir: {baseDir}");
    Console.WriteLine($"ExePath: {exePath}");
    Console.WriteLine($"ExeDir: {exeDir}");
    Console.WriteLine($"Exe Exists: {File.Exists(exePath)}");
    Console.WriteLine("====================");

    if (!File.Exists(exePath))
        return Results.NotFound($"EXE не найден: {exePath}");

    var psi = new ProcessStartInfo
    {
        FileName = exePath,
        Arguments = request.SessionId,
        WorkingDirectory = exeDir,
        UseShellExecute = true
    };

    var process = Process.Start(psi);

    Console.WriteLine($"Process started: {process != null}");
    Console.WriteLine($"ProcessId: {process?.Id}");

    return Results.Ok(new
    {
        message = "Запущено",
        sessionId = request.SessionId,
        exePath,
        exeDir,
        processId = process?.Id
    });
});

app.MapPost("/word-status-update", (WordStatusUpdateRequest dto) =>
{
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
    if (!statusStore.TryGetValue(sessionId, out var status))
        return Results.NotFound("Статус не найден");

    return Results.Ok(status);
});

app.MapGet("/", () => "API работает");

app.Run("http://localhost:5298");

public class WordStatusUpdateRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsWordOpen { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class OpenAppRequest
{
    public string SessionId { get; set; } = string.Empty;
}