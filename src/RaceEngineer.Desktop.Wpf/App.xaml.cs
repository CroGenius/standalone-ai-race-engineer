using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace RaceEngineer.Desktop.Wpf;

public partial class App : Application
{
    private static readonly List<string> PendingStartupWarningList = [];

    internal static IReadOnlyList<string> PendingStartupWarnings => PendingStartupWarningList;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var blockedFiles = BlockedFileStartupCheck.FindBlockedFiles(AppContext.BaseDirectory);
        var blockedWarning = BlockedFileStartupCheck.BuildWarningMessage(blockedFiles);
        if (!string.IsNullOrWhiteSpace(blockedWarning))
        {
            PendingStartupWarningList.Add(blockedWarning);
        }

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            FormatExceptionDetails(e.Exception),
            "Race Engineer startup/runtime error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            MessageBox.Show(
                FormatExceptionDetails(exception),
                "Race Engineer unhandled error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    internal static string FormatExceptionDetails(Exception exception)
    {
        var builder = new StringBuilder();
        var policyHint = BlockedFileStartupCheck.TryBuildFileLoadPolicyHint(exception);
        if (!string.IsNullOrWhiteSpace(policyHint))
        {
            builder.AppendLine(policyHint);
            builder.AppendLine();
        }

        var current = exception;
        var depth = 0;
        while (current is not null && depth < 6)
        {
            if (depth == 0)
            {
                builder.AppendLine(current.Message);
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine($"Inner exception ({depth}): {current.GetType().Name}");
                builder.AppendLine(current.Message);
            }

            if (!string.IsNullOrWhiteSpace(current.StackTrace) && depth == 0)
            {
                builder.AppendLine();
                builder.AppendLine(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        return builder.ToString().Trim();
    }
}
