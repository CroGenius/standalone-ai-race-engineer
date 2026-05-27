using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RaceEngineer.Core.TelemetryVisualization;

namespace RaceEngineer.Desktop.Wpf;

public partial class TelemetryTimelineControl : UserControl
{
    public static readonly DependencyProperty TimelineProperty = DependencyProperty.Register(
        nameof(Timeline),
        typeof(TelemetryTimeline),
        typeof(TelemetryTimelineControl),
        new PropertyMetadata(TelemetryTimeline.Empty, OnTimelineChanged));

    public static readonly DependencyProperty CursorProgressProperty = DependencyProperty.Register(
        nameof(CursorProgress),
        typeof(double),
        typeof(TelemetryTimelineControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCursorChanged));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(TelemetryTimelineControl),
        new PropertyMetadata("Telemetry Traces"));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText),
        typeof(string),
        typeof(TelemetryTimelineControl),
        new PropertyMetadata("Waiting for telemetry snapshots."));

    public TelemetryTimelineControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderTimeline();
        SizeChanged += (_, _) => RenderTimeline();
    }

    public TelemetryTimeline Timeline
    {
        get => (TelemetryTimeline)GetValue(TimelineProperty);
        set => SetValue(TimelineProperty, value);
    }

    public double CursorProgress
    {
        get => (double)GetValue(CursorProgressProperty);
        set => SetValue(CursorProgressProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    private static void OnTimelineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TelemetryTimelineControl control && e.NewValue is TelemetryTimeline timeline)
        {
            control.Title = timeline.Title;
            control.StatusText = timeline.StatusText;
            if (Math.Abs(control.CursorProgress - timeline.CursorProgress) > 0.0001)
            {
                control.CursorProgress = timeline.CursorProgress;
            }

            control.RenderTimeline();
        }
    }

    private static void OnCursorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TelemetryTimelineControl control)
        {
            control.RenderTimeline();
        }
    }

    private void RenderTimeline()
    {
        TraceRowsHost.Items.Clear();
        var timeline = Timeline ?? TelemetryTimeline.Empty;
        foreach (var row in timeline.Rows)
        {
            TraceRowsHost.Items.Add(BuildRowPanel(row, timeline.Markers, CursorProgress));
        }
    }

    private UIElement BuildRowPanel(TraceRow row, IReadOnlyList<TimelineMarker> markers, double cursorProgress)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

        var header = new DockPanel();
        header.Children.Add(new TextBlock
        {
            Text = row.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0xF3, 0xF4))
        });
        var legend = string.Join(" | ", row.Series.Select(series => series.Label));
        var legendBlock = new TextBlock
        {
            Text = legend,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA6, 0xAC)),
            FontSize = 11
        };
        DockPanel.SetDock(legendBlock, Dock.Right);
        header.Children.Add(legendBlock);
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var canvas = new Canvas
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x19, 0x1D)),
            ClipToBounds = true
        };
        canvas.SizeChanged += (_, _) => DrawRow(canvas, row, markers, cursorProgress);
        Grid.SetRow(canvas, 1);
        grid.Children.Add(canvas);
        return grid;
    }

    private static void DrawRow(Canvas canvas, TraceRow row, IReadOnlyList<TimelineMarker> markers, double cursorProgress)
    {
        canvas.Children.Clear();
        var width = Math.Max(canvas.ActualWidth, 1);
        var height = Math.Max(canvas.ActualHeight, 1);

        foreach (var marker in markers)
        {
            var x = marker.Progress * width;
            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = marker.Category switch
                {
                    "Sector" => new SolidColorBrush(Color.FromArgb(60, 0x90, 0xA4, 0xAE)),
                    "Corner" => new SolidColorBrush(Color.FromArgb(80, 0xF5, 0xA5, 0x24)),
                    _ => new SolidColorBrush(Color.FromArgb(90, 0x5B, 0xC0, 0xFF))
                },
                StrokeThickness = marker.Category == "Sector" ? 1 : 1.5,
                StrokeDashArray = marker.Category == "Sector" ? [3, 3] : null
            };
            canvas.Children.Add(line);
        }

        foreach (var series in row.Series)
        {
            var polyline = new Polyline
            {
                Stroke = (Brush)new BrushConverter().ConvertFromString(series.ColorHint)!,
                StrokeThickness = series.IsOverlay ? 1.5 : 2,
                StrokeDashArray = series.IsOverlay ? [4, 3] : null,
                Fill = Brushes.Transparent,
                Opacity = series.IsOverlay ? 0.85 : 1.0
            };

            var points = new PointCollection();
            foreach (var point in series.Points)
            {
                var x = point.Progress * width;
                var normalized = (point.Value - row.MinValue) / (row.MaxValue - row.MinValue);
                var y = height - (normalized * (height - 4)) - 2;
                points.Add(new Point(x, y));
            }

            polyline.Points = points;
            canvas.Children.Add(polyline);
        }

        var cursorX = cursorProgress * width;
        canvas.Children.Add(new Line
        {
            X1 = cursorX,
            X2 = cursorX,
            Y1 = 0,
            Y2 = height,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Opacity = 0.9
        });
    }
}
