using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StudyRoom.Core.Models;

namespace StudyRoom.UI.Views.Components;

public partial class WeeklyActivityChart : UserControl
{
    public static readonly DependencyProperty ActivityPointsProperty =
        DependencyProperty.Register(
            nameof(ActivityPoints),
            typeof(IEnumerable<DailyActivityPoint>),
            typeof(WeeklyActivityChart),
            new PropertyMetadata(null, OnActivityPointsChanged));

    public IEnumerable<DailyActivityPoint>? ActivityPoints
    {
        get => (IEnumerable<DailyActivityPoint>?)GetValue(ActivityPointsProperty);
        set => SetValue(ActivityPointsProperty, value);
    }

    private static void OnActivityPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WeeklyActivityChart chart)
        {
            if (e.OldValue is INotifyCollectionChanged oldColl)
            {
                oldColl.CollectionChanged -= chart.OnCollectionChanged;
            }
            if (e.NewValue is INotifyCollectionChanged newColl)
            {
                newColl.CollectionChanged += chart.OnCollectionChanged;
            }
            chart.RedrawChart();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawChart();
    }

    public WeeklyActivityChart()
    {
        InitializeComponent();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawChart();
    }

    private void RedrawChart()
    {
        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;

        if (width <= 10 || height <= 10)
        {
            return;
        }

        // Update horizontal grid lines
        UpdateGridLines(width, height);

        var pointsList = ActivityPoints?.ToList();
        if (pointsList == null || pointsList.Count == 0)
        {
            // Default 7 points if null or empty
            pointsList = [
                new DailyActivityPoint { DayLabel = "T2", Hours = 3.5 },
                new DailyActivityPoint { DayLabel = "T3", Hours = 5.0 },
                new DailyActivityPoint { DayLabel = "T4", Hours = 4.2 },
                new DailyActivityPoint { DayLabel = "T5", Hours = 6.8 },
                new DailyActivityPoint { DayLabel = "T6", Hours = 4.0 },
                new DailyActivityPoint { DayLabel = "T7", Hours = 7.5 },
                new DailyActivityPoint { DayLabel = "CN", Hours = 4.5 }
            ];
        }

        const double maxHours = 8.0;
        int count = pointsList.Count;
        double stepX = count > 1 ? width / (count - 1) : width;

        const double paddingTop = 20.0;
        const double paddingBottom = 8.0;
        double usableHeight = Math.Max(10.0, height - paddingTop - paddingBottom);

        var screenPoints = new List<Point>(count);
        for (int i = 0; i < count; i++)
        {
            double x = i * stepX;
            double hours = Math.Clamp(pointsList[i].Hours, 0.0, maxHours);
            double y = paddingTop + (usableHeight - (hours / maxHours * usableHeight));
            screenPoints.Add(new Point(x, y));
        }

        // Build smooth Bézier spline
        var curveGeometry = BuildSmoothCurveGeometry(screenPoints);
        CurvePath.Data = curveGeometry;

        // Build filled area geometry
        var areaGeometry = BuildAreaGeometry(screenPoints, width, height);
        AreaPath.Data = areaGeometry;

        // Populate dots
        DotsLayer.Children.Clear();
        int highlightIndex = 0;
        double maxH = -1;
        for (int i = 0; i < pointsList.Count; i++)
        {
            if (pointsList[i].Hours > maxH)
            {
                maxH = pointsList[i].Hours;
                highlightIndex = i;
            }
        }

        for (int i = 0; i < screenPoints.Count; i++)
        {
            var pt = screenPoints[i];
            bool isHighlight = (i == highlightIndex);

            var dot = new Ellipse
            {
                Width = isHighlight ? 12 : 8,
                Height = isHighlight ? 12 : 8,
                Fill = isHighlight ? new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)) : new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1)),
                Stroke = Brushes.White,
                StrokeThickness = isHighlight ? 2.5 : 1.5
            };

            Canvas.SetLeft(dot, pt.X - (dot.Width / 2));
            Canvas.SetTop(dot, pt.Y - (dot.Height / 2));
            DotsLayer.Children.Add(dot);

            if (isHighlight)
            {
                // Position guide line
                HighlightGuideLine.X1 = pt.X;
                HighlightGuideLine.Y1 = pt.Y;
                HighlightGuideLine.X2 = pt.X;
                HighlightGuideLine.Y2 = paddingTop + usableHeight;
                HighlightGuideLine.Visibility = Visibility.Visible;

                // Position tooltip
                TooltipText.Text = $"{pointsList[i].Hours:F1}h";
                TooltipDayText.Text = $" • {pointsList[i].DayLabel}";
                HighlightTooltip.Visibility = Visibility.Visible;
                Canvas.SetLeft(HighlightTooltip, Math.Clamp(pt.X - 35, 0, width - 80));
                Canvas.SetTop(HighlightTooltip, Math.Max(0, pt.Y - 36));
            }
        }

        // Dynamically update X-axis labels
        XAxisGrid.Children.Clear();
        for (int i = 0; i < pointsList.Count; i++)
        {
            var tb = new TextBlock
            {
                Text = pointsList[i].DayLabel,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            if (i == highlightIndex)
            {
                tb.FontWeight = FontWeights.SemiBold;
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
                tb.Opacity = 1.0;
            }
            else
            {
                tb.FontWeight = FontWeights.Normal;
                tb.Opacity = 0.6;
            }
            XAxisGrid.Children.Add(tb);
        }
    }

    private void UpdateGridLines(double width, double height)
    {
        const double paddingTop = 20.0;
        const double paddingBottom = 8.0;
        double usableHeight = Math.Max(10.0, height - paddingTop - paddingBottom);

        GridLineTop.X1 = 0; GridLineTop.Y1 = paddingTop; GridLineTop.X2 = width; GridLineTop.Y2 = paddingTop;
        GridLineMidHigh.X1 = 0; GridLineMidHigh.Y1 = paddingTop + usableHeight * 0.25; GridLineMidHigh.X2 = width; GridLineMidHigh.Y2 = paddingTop + usableHeight * 0.25;
        GridLineMid.X1 = 0; GridLineMid.Y1 = paddingTop + usableHeight * 0.5; GridLineMid.X2 = width; GridLineMid.Y2 = paddingTop + usableHeight * 0.5;
        GridLineMidLow.X1 = 0; GridLineMidLow.Y1 = paddingTop + usableHeight * 0.75; GridLineMidLow.X2 = width; GridLineMidLow.Y2 = paddingTop + usableHeight * 0.75;
        GridLineBottom.X1 = 0; GridLineBottom.Y1 = paddingTop + usableHeight; GridLineBottom.X2 = width; GridLineBottom.Y2 = paddingTop + usableHeight;
    }

    private static PathGeometry BuildSmoothCurveGeometry(IReadOnlyList<Point> points)
    {
        var geometry = new PathGeometry();
        if (points.Count == 0) return geometry;

        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = false,
            IsFilled = false
        };

        if (points.Count == 1)
        {
            geometry.Figures.Add(figure);
            return geometry;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : p2;

            double c1X = p1.X + (p2.X - p0.X) / 6.0;
            double c1Y = p1.Y + (p2.Y - p0.Y) / 6.0;

            double c2X = p2.X - (p3.X - p1.X) / 6.0;
            double c2Y = p2.Y - (p3.Y - p1.Y) / 6.0;

            figure.Segments.Add(new BezierSegment(new Point(c1X, c1Y), new Point(c2X, c2Y), p2, true));
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    private static PathGeometry BuildAreaGeometry(IReadOnlyList<Point> points, double width, double height)
    {
        var geometry = new PathGeometry();
        if (points.Count == 0) return geometry;

        const double paddingTop = 20.0;
        const double paddingBottom = 8.0;
        double bottomY = paddingTop + Math.Max(10.0, height - paddingTop - paddingBottom);

        var figure = new PathFigure
        {
            StartPoint = points[0],
            IsClosed = true,
            IsFilled = true
        };

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : p2;

            double c1X = p1.X + (p2.X - p0.X) / 6.0;
            double c1Y = p1.Y + (p2.Y - p0.Y) / 6.0;

            double c2X = p2.X - (p3.X - p1.X) / 6.0;
            double c2Y = p2.Y - (p3.Y - p1.Y) / 6.0;

            figure.Segments.Add(new BezierSegment(new Point(c1X, c1Y), new Point(c2X, c2Y), p2, true));
        }

        // Close along the bottom axis
        figure.Segments.Add(new LineSegment(new Point(points[^1].X, bottomY), true));
        figure.Segments.Add(new LineSegment(new Point(points[0].X, bottomY), true));

        geometry.Figures.Add(figure);
        return geometry;
    }
}
