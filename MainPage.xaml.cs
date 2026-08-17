using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Padallock;

public sealed partial class MainPage : Page
{
    private PadallockController Controller => ((App)Application.Current).Controller;
    private bool _isInitializing;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        Controller.StatusChanged += Controller_StatusChanged;
        await Controller.InitializeAsync();
        EnabledSwitch.IsOn = Controller.Settings.IsEnabled;
        TrackDetailsSwitch.IsOn = Controller.Settings.ShowTrackDetails;
        _isInitializing = false;
    }

    private void Controller_StatusChanged(object? sender, PadallockStatus status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusInfoBar.Message = status.Message;
            StatusInfoBar.Severity = status.IsError ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
            PlaybackStatusTextBlock.Text = status.PlaybackDescription;
        });
    }

    private async void EnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        EnabledSwitch.IsEnabled = false;
        await Controller.SetEnabledAsync(EnabledSwitch.IsOn);
        EnabledSwitch.IsOn = Controller.Settings.IsEnabled;
        EnabledSwitch.IsEnabled = true;
    }

    private async void TrackDetailsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
        {
            return;
        }

        await Controller.SetShowTrackDetailsAsync(TrackDetailsSwitch.IsOn);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        await Controller.RefreshAsync();
        RefreshButton.IsEnabled = true;
    }
}
