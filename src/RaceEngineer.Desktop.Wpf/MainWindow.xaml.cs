using System.Windows;
using System.Windows.Input;

namespace RaceEngineer.Desktop.Wpf;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        Closing += async (_, _) => await viewModel.StopAsync();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (PushToTalkButton is null)
            {
                viewModel.ReportStartupWarning("Push-to-talk button was not found in the UI.");
                return;
            }

            PushToTalkButton.PreviewMouseLeftButtonDown += (_, _) => viewModel.BeginPushToTalk();
            PushToTalkButton.PreviewMouseLeftButtonUp += (_, _) => viewModel.EndPushToTalk();
            PushToTalkButton.MouseLeave += (_, _) => viewModel.EndPushToTalk();
        }
        catch (Exception exception)
        {
            viewModel.ReportStartupWarning($"Push-to-talk UI wiring failed. Hotkey-only mode remains available. {exception.Message}");
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat || !viewModel.MatchesPushToTalkHotkey(e))
        {
            return;
        }

        viewModel.BeginPushToTalk();
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!viewModel.MatchesPushToTalkHotkey(e))
        {
            return;
        }

        viewModel.EndPushToTalk();
        e.Handled = true;
    }
}
