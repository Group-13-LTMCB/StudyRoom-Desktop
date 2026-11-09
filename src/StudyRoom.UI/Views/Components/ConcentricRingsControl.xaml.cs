using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StudyRoom.UI.Views.Components;

public partial class ConcentricRingsControl : UserControl
{
    public static readonly DependencyProperty FocusProgressProperty =
        DependencyProperty.Register(nameof(FocusProgress), typeof(double), typeof(ConcentricRingsControl),
            new PropertyMetadata(0.75, OnProgressChanged));

    public static readonly DependencyProperty TasksProgressProperty =
        DependencyProperty.Register(nameof(TasksProgress), typeof(double), typeof(ConcentricRingsControl),
            new PropertyMetadata(0.60, OnProgressChanged));

    public static readonly DependencyProperty StreakProgressProperty =
        DependencyProperty.Register(nameof(StreakProgress), typeof(double), typeof(ConcentricRingsControl),
            new PropertyMetadata(0.85, OnProgressChanged));

    public double FocusProgress
    {
        get => (double)GetValue(FocusProgressProperty);
        set => SetValue(FocusProgressProperty, value);
    }

    public double TasksProgress
    {
        get => (double)GetValue(TasksProgressProperty);
        set => SetValue(TasksProgressProperty, value);
    }

    public double StreakProgress
    {
        get => (double)GetValue(StreakProgressProperty);
        set => SetValue(StreakProgressProperty, value);
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConcentricRingsControl control)
        {
            control.DrawRings();
        }
    }

    public ConcentricRingsControl()
    {
        InitializeComponent();
        Loaded += (_, _) => DrawRings();
    }

    public void DrawRings()
    {
        RingsCanvas.Children.Clear();

        double center = 80;
        double strokeWidth = 8;

        // 1. Outer Ring: Focus Progress (Color: #4F46E5)
        DrawRingLayer(center, radius: 68, strokeWidth, "#4F46E5", FocusProgress);

        // 2. Middle Ring: Tasks Progress (Color: #10B981)
        DrawRingLayer(center, radius: 52, strokeWidth, "#10B981", TasksProgress);

        // 3. Inner Ring: Streak Progress (Color: #F59E0B)
        DrawRingLayer(center, radius: 36, strokeWidth, "#F59E0B", StreakProgress);
    }

    private void DrawRingLayer(double center, double radius, double stroke, string colorHex, double progress)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        var trackBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)) { Opacity = 0.15 };

        // Background Track Full Circle
        var trackCircle = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = trackBrush,
            StrokeThickness = stroke
        };
        Canvas.SetLeft(trackCircle, center - radius);
        Canvas.SetTop(trackCircle, center - radius);
        RingsCanvas.Children.Add(trackCircle);

        // Foreground Active Arc
        double clamped = Math.Clamp(progress, 0.01, 0.999);
        double angle = clamped * 360.0;

        // Angle in radians (starting from -90 degrees / 12 o'clock)
        double startAngle = -90;
        double endAngle = startAngle + angle;

        Point startPoint = PointOnCircle(center, radius, startAngle);
        Point endPoint = PointOnCircle(center, radius, endAngle);

        var arc = new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = angle > 180
        };

        var figure = new PathFigure
        {
            StartPoint = startPoint,
            Segments = [arc],
            IsClosed = false
        };

        var geometry = new PathGeometry([figure]);

        var path = new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = stroke,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        RingsCanvas.Children.Add(path);
    }

    private static Point PointOnCircle(double center, double radius, double angleInDegrees)
    {
        double angleInRadians = angleInDegrees * Math.PI / 180.0;
        return new Point(
            center + (radius * Math.Cos(angleInRadians)),
            center + (radius * Math.Sin(angleInRadians)));
    }
}