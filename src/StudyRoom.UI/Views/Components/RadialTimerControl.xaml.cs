using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StudyRoom.UI.Views.Components;

public partial class RadialTimerControl : UserControl
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(RadialTimerControl),
            new FrameworkPropertyMetadata(1.0, OnProgressChanged));

    public static readonly DependencyProperty FormattedTimeProperty =
        DependencyProperty.Register(
            nameof(FormattedTime),
            typeof(string),
            typeof(RadialTimerControl),
            new FrameworkPropertyMetadata("25:00", OnFormattedTimeChanged));

    public static readonly DependencyProperty PhaseTextProperty =
        DependencyProperty.Register(
            nameof(PhaseText),
            typeof(string),
            typeof(RadialTimerControl),
            new FrameworkPropertyMetadata("🎯 Pha Tập Trung (Focus)", OnPhaseTextChanged));

    public static readonly DependencyProperty PhaseColorProperty =
        DependencyProperty.Register(
            nameof(PhaseColor),
            typeof(Brush),
            typeof(RadialTimerControl),
            new FrameworkPropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")), OnPhaseColorChanged));

    public static readonly DependencyProperty CycleTextProperty =
        DependencyProperty.Register(
            nameof(CycleText),
            typeof(string),
            typeof(RadialTimerControl),
            new FrameworkPropertyMetadata("Chu kỳ: 1 / 4", OnCycleTextChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string FormattedTime
    {
        get => (string)GetValue(FormattedTimeProperty);
        set => SetValue(FormattedTimeProperty, value);
    }

    public string PhaseText
    {
        get => (string)GetValue(PhaseTextProperty);
        set => SetValue(PhaseTextProperty, value);
    }

    public Brush PhaseColor
    {
        get => (Brush)GetValue(PhaseColorProperty);
        set => SetValue(PhaseColorProperty, value);
    }

    public string CycleText
    {
        get => (string)GetValue(CycleTextProperty);
        set => SetValue(CycleTextProperty, value);
    }

    public RadialTimerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RedrawArc();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawArc();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTimerControl control)
        {
            control.RedrawArc();
        }
    }

    private static void OnFormattedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTimerControl control && e.NewValue is string text)
        {
            control.TimeTextBlock.Text = text;
        }
    }

    private static void OnPhaseTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTimerControl control && e.NewValue is string text)
        {
            control.PhaseTextBlock.Text = text;
        }
    }

    private static void OnPhaseColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTimerControl control && e.NewValue is Brush brush)
        {
            control.ProgressPath.Stroke = brush;
            control.PhaseTextBlock.Foreground = brush;
        }
    }

    private static void OnCycleTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTimerControl control && e.NewValue is string text)
        {
            control.CycleTextBlock.Text = text;
        }
    }

    private void RedrawArc()
    {
        double width = ArcCanvas.ActualWidth > 0 ? ArcCanvas.ActualWidth : 340;
        double height = ArcCanvas.ActualHeight > 0 ? ArcCanvas.ActualHeight : 340;

        double strokeThickness = 12.0;
        double cx = width / 2.0;
        double cy = height / 2.0;
        double radius = Math.Max(10.0, Math.Min(cx, cy) - strokeThickness);

        // Update track ellipse position
        TrackEllipse.Width = radius * 2.0;
        TrackEllipse.Height = radius * 2.0;
        Canvas.SetLeft(TrackEllipse, cx - radius);
        Canvas.SetTop(TrackEllipse, cy - radius);

        double fraction = Math.Clamp(Progress, 0.0, 1.0);
        if (fraction <= 0.001)
        {
            ProgressPath.Data = null;
            return;
        }

        // Full circle case
        if (fraction >= 0.999)
        {
            var ellipseGeo = new EllipseGeometry(new Point(cx, cy), radius, radius);
            ProgressPath.Data = ellipseGeo;
            return;
        }

        double angle = fraction * 360.0;
        double angleRad = (angle - 90.0) * Math.PI / 180.0;

        var startPoint = new Point(cx, cy - radius);
        var endPoint = new Point(cx + radius * Math.Cos(angleRad), cy + radius * Math.Sin(angleRad));

        bool isLargeArc = angle > 180.0;

        var segment = new ArcSegment(
            endPoint,
            new Size(radius, radius),
            0.0,
            isLargeArc,
            SweepDirection.Clockwise,
            true);

        var figure = new PathFigure
        {
            StartPoint = startPoint,
            IsClosed = false
        };
        figure.Segments.Add(segment);

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        ProgressPath.Data = geometry;
    }
}
