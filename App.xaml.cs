using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Padallock;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Controller = new PadallockController(
            new SettingsStore(),
            new MediaSessionService(),
            new LockScreenWallpaperService(),
            new ArtworkRenderer());
    }

    public PadallockController Controller { get; }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask)
        {
            await Controller.InitializeAsync();
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
