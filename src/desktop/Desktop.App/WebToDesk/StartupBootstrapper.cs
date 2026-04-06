using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;

namespace WebToDesk;

internal readonly record struct StartupProgress(string Message, int ProgressPercent);

internal static class StartupBootstrapper
{
    private const string MinioHealthUrl = "http://127.0.0.1:9000/minio/health/live";
    private const string WebHealthUrl = "http://127.0.0.1:5000/health";
    private const string AppUrl = "http://localhost:5000";

    private static readonly HttpClient StatusClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private static readonly SemaphoreSlim ServerLock = new(1, 1);

    private static Task? _webServerTask;
    private static bool _browserOpened;

    public static async Task InitializeAsync(
        WebApplication app,
        IProgress<StartupProgress> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new StartupProgress("Проверяем локальное хранилище...", 12));
        await EnsureMinioReadyAsync(progress, cancellationToken);

        progress.Report(new StartupProgress("Поднимаем локальный веб-сервер...", 72));
        await EnsureWebServerReadyAsync(app, progress, cancellationToken);

        progress.Report(new StartupProgress("Открываем WebToDesk в браузере...", 95));
        OpenBrowserOnce();

        progress.Report(new StartupProgress("Готово к работе.", 100));
    }

    private static async Task EnsureMinioReadyAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken)
    {
        if (!await IsUrlReadyAsync(MinioHealthUrl, cancellationToken))
        {
            var minioPath = AppLocator.TryFindFile("minio.exe");
            if (string.IsNullOrWhiteSpace(minioPath))
            {
                throw new InvalidOperationException(
                    "Не найден minio.exe. Положите его рядом с приложением или в корень проекта.");
            }

            var workingDirectory = Path.GetDirectoryName(minioPath)!;
            var dataDirectory = Path.Combine(workingDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            progress.Report(new StartupProgress("Запускаем MinIO без консоли...", 24));
            StartDetachedProcess(
                minioPath,
                $"server \"{dataDirectory}\" --console-address \":9001\"",
                workingDirectory,
                new Dictionary<string, string>
                {
                    ["MINIO_ROOT_USER"] = "minioadmin",
                    ["MINIO_ROOT_PASSWORD"] = "minioadmin"
                });

            await WaitForUrlAsync(
                MinioHealthUrl,
                TimeSpan.FromSeconds(40),
                "MinIO не успел подняться.",
                cancellationToken);
        }

        progress.Report(new StartupProgress("Настраиваем бакет для локального режима...", 48));
        await EnsureBucketConfiguredAsync(progress, cancellationToken);
    }

    private static async Task EnsureBucketConfiguredAsync(IProgress<StartupProgress> progress, CancellationToken cancellationToken)
    {
        var mcPath = AppLocator.TryFindFile("mc.exe");
        if (string.IsNullOrWhiteSpace(mcPath))
        {
            progress.Report(new StartupProgress("mc.exe не найден, пропускаем автонастройку бакета.", 60));
            return;
        }

        var workingDirectory = Path.GetDirectoryName(mcPath)!;

        await RunHiddenProcessAsync(
            mcPath,
            "alias set myminio http://127.0.0.1:9000/ minioadmin minioadmin",
            workingDirectory,
            cancellationToken);

        await RunHiddenProcessAsync(
            mcPath,
            "mb --ignore-existing myminio/bucket",
            workingDirectory,
            cancellationToken);

        await RunHiddenProcessAsync(
            mcPath,
            "anonymous set public myminio/bucket",
            workingDirectory,
            cancellationToken);

        progress.Report(new StartupProgress("Локальное хранилище готово.", 60));
    }

    private static async Task EnsureWebServerReadyAsync(
        WebApplication app,
        IProgress<StartupProgress> progress,
        CancellationToken cancellationToken)
    {
        await EnsureWebServerStartedAsync(app, cancellationToken);

        await WaitForUrlAsync(
            WebHealthUrl,
            TimeSpan.FromSeconds(30),
            "Локальный сервер не ответил вовремя.",
            cancellationToken);

        progress.Report(new StartupProgress("Интерфейс готов, завершаем запуск...", 90));
    }

    private static async Task EnsureWebServerStartedAsync(WebApplication app, CancellationToken cancellationToken)
    {
        await ServerLock.WaitAsync(cancellationToken);
        try
        {
            if (_webServerTask is null)
            {
                _webServerTask = Task.Run(() => app.RunAsync(AppUrl), CancellationToken.None);
            }
        }
        finally
        {
            ServerLock.Release();
        }
    }

    private static async Task<bool> IsUrlReadyAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await StatusClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForUrlAsync(
        string url,
        TimeSpan timeout,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsUrlReadyAsync(url, cancellationToken))
            {
                return;
            }

            await Task.Delay(700, cancellationToken);
        }

        throw new TimeoutException(errorMessage);
    }

    private static async Task RunHiddenProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode == 0)
        {
            return;
        }

        var error = await process.StandardError.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(error))
        {
            error = await process.StandardOutput.ReadToEndAsync();
        }

        throw new InvalidOperationException(
            $"Команда {Path.GetFileName(fileName)} завершилась с кодом {process.ExitCode}. {error}".Trim());
    }

    private static void StartDetachedProcess(
        string fileName,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        Process.Start(startInfo);
    }

    private static void OpenBrowserOnce()
    {
        if (_browserOpened)
        {
            return;
        }

        _browserOpened = true;

        Process.Start(new ProcessStartInfo
        {
            FileName = AppUrl,
            UseShellExecute = true
        });
    }
}
