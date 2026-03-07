namespace WebToDesk;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Если API передал sessionId в аргументах
        // берём его. Если нет, создаём новый.
        var sessionId = args.Length > 0
            ? args[0]
            : Guid.NewGuid().ToString();

        Application.Run(new Form1(sessionId));
    }
}