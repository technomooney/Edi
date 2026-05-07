using Avalonia;
using Edi.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Edi.Avalonia;

class Program
{
    public static IHost? AppHost { get; private set; }

    // STAThread is still needed on Windows; Avalonia ignores it on Linux/macOS
    [STAThread]
    public static void Main(string[] args)
    {
        bool createdNew;
        using var mutex = new Mutex(true, "Edi", out createdNew);
        if (!createdNew)
            return;

        AppHost = Host.CreateDefaultBuilder()
            .UseSerilog((ctx, sp, loggerConfig) =>
            {
                var ediConfig = sp.GetService<ConfigurationManager>()!.Get<EdiConfig>();
                loggerConfig
                    .MinimumLevel.Debug()
                    .WriteTo.Conditional(
                        _ => ediConfig.UseLogs,
                        wt => wt.File("./Edilog.txt",
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 1));
            })
            .ConfigureServices((_, services) =>
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "EdiConfig.json");
                services.AddEdi(configPath);
            })
            .Build();

        AppHost.StartAsync().GetAwaiter().GetResult();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        AppHost.StopAsync().GetAwaiter().GetResult();
        AppHost.Dispose();
    }

    static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
