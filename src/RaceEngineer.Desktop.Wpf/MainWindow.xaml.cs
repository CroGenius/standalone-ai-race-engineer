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
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        PushToTalkButton.PreviewMouseLeftButtonDown += (_, _) => viewModel.BeginPushToTalk();
        PushToTalkButton.PreviewMouseLeftButtonUp += (_, _) => viewModel.EndPushToTalk();
        PushToTalkButton.MouseLeave += (_, _) => viewModel.EndPushToTalk();
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
