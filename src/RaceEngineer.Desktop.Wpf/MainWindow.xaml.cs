using System.Windows;

namespace RaceEngineer.Desktop.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        Closing += async (_, _) => await viewModel.StopAsync();
    }
}
