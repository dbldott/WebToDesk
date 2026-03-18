using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();

var app = builder.Build();

app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

var statusStore = new Dictionary<string, WordStatusUpdateRequest>();

app.MapPost("/open-app", (OpenAppRequest request) =>
{
    var user = Environment.UserName;

    // var exePath = $@"C:\Sayat\C_Sharp\WebToDesk\src\desktop\Desktop.App\WebToDesk\bin\Debug\net10.0-windows\WebToDesk.exe";
    var exePath = @"C:\Sayat\C_Sharp\WebToDesk\src\desktop\Desktop.App\WebToDesk\bin\Debug\net10.0-windows\WebToDesk.exe";
    if (!File.Exists(exePath))
    {
        return Results.NotFound("EXE не найден");
    }

    Console.WriteLine($"OPEN-APP SessionId: {request.SessionId}");

    Process.Start(new ProcessStartInfo
    {
        FileName = exePath,
        Arguments = request.SessionId,
        UseShellExecute = true
    });

    return Results.Ok(new
    {
        message = "Запущено",
        sessionId = request.SessionId
    });
});

app.MapPost("/word-status-update", (WordStatusUpdateRequest dto) =>
{
    statusStore[dto.SessionId] = dto;

    Console.WriteLine("----- WORD STATUS UPDATE -----");
    Console.WriteLine($"SessionId: {dto.SessionId}");
    Console.WriteLine($"Status: {dto.Status}");
    Console.WriteLine($"IsWordOpen: {dto.IsWordOpen}");

    // Чтобы лог был понятнее, лучше явно форматировать
    Console.WriteLine($"StartedAt: {dto.StartedAt:yyyy-MM-dd HH:mm:ss zzz}");
    Console.WriteLine($"ClosedAt: {dto.ClosedAt:yyyy-MM-dd HH:mm:ss zzz}");

    Console.WriteLine($"ErrorMessage: {dto.ErrorMessage}");
    Console.WriteLine("------------------------------");

    return Results.Ok(new { message = "Статус получен" });
});

app.MapGet("/word-status/{sessionId}", (string sessionId) =>
{
    if (!statusStore.TryGetValue(sessionId, out var status))
    {
        return Results.NotFound("Статус не найден");
    }

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







