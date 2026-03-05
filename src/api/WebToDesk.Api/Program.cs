using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(); // Добавляем CORS

var app = builder.Build();

// Разрешаем всё для тестов
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.MapPost("/open-app", () =>
{
    var user = Environment.UserName;

    var exePath = $@"C:\Users\{user}\prjct\WebToDesk\src\desktop\Desktop.App\WebToDesk\bin\Debug\net10.0-windows\WebToDesk.exe";
    if (!File.Exists(exePath)) return Results.NotFound("EXE не найден");

    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
    return Results.Ok("Запущено");
});

// Явно заставляем слушать этот порт на всех интерфейсах
app.Run("http://localhost:5298");