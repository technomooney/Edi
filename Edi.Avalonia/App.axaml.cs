using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Edi.Avalonia.Forms;
using Edi.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Edi.Avalonia;

[PropertyChanged.DoNotNotify]
public partial class App : Application
{
    public static IEdi? Edi { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Edi = Program.AppHost!.Services.GetRequiredService<IEdi>();
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
