using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StudyRoom.UI.Views.Components;

public partial class TaskCalendarControl : UserControl
{
    private DateTime _displayDate;

    public TaskCalendarControl()
    {
        InitializeComponent();
        _displayDate = DateTime.Today;
        RenderCalendar();
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayDate = _displayDate.AddMonths(-1);
        RenderCalendar();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayDate = _displayDate.AddMonths(1);
        RenderCalendar();
    }

    public void RenderCalendar()
    {
        MonthYearText.Text = _displayDate.ToString("MMMM, yyyy", new CultureInfo("vi-VN"));

        DaysGrid.Children.Clear();

        var firstDayOfMonth = new DateTime(_displayDate.Year, _displayDate.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_displayDate.Year, _displayDate.Month);

        // DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
        // In Vietnamese / ISO: Monday is first day (0), Sunday is last (6)
        int startOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;

        var prevMonthDate = _displayDate.AddMonths(-1);
        int daysInPrevMonth = DateTime.DaysInMonth(prevMonthDate.Year, prevMonthDate.Month);

        // Days from previous month
        for (int i = 0; i < startOffset; i++)
        {
            int dayNumber = daysInPrevMonth - startOffset + 1 + i;
            DaysGrid.Children.Add(CreateDayCell(dayNumber.ToString(), isCurrentMonth: false, isToday: false, hasEvent: false));
        }

        // Days of current month
        var today = DateTime.Today;
        for (int day = 1; day <= daysInMonth; day++)
        {
            bool isToday = (today.Year == _displayDate.Year && today.Month == _displayDate.Month && today.Day == day);
            bool hasEvent = (day % 3 == 0 || isToday); // Sample scheduled days

            DaysGrid.Children.Add(CreateDayCell(day.ToString(), isCurrentMonth: true, isToday: isToday, hasEvent: hasEvent));
        }

        // Remaining trailing cells to fill 42 cells (6 rows * 7 columns)
        int totalCells = DaysGrid.Children.Count;
        int nextDay = 1;
        while (totalCells < 42)
        {
            DaysGrid.Children.Add(CreateDayCell(nextDay.ToString(), isCurrentMonth: false, isToday: false, hasEvent: false));
            nextDay++;
            totalCells++;
        }
    }

    private UIElement CreateDayCell(string text, bool isCurrentMonth, bool isToday, bool hasEvent)
    {
        var container = new Grid
        {
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(1)
        };

        if (isToday)
        {
            // Highlight today with solid dark circle pill (Reference Image 3 style)
            var pill = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B")), // Dark zinc pill
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            container.Children.Add(pill);

            var dayText = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            container.Children.Add(dayText);
        }
        else
        {
            var dayText = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = isCurrentMonth ? FontWeights.Medium : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isCurrentMonth)
            {
                dayText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            }
            else
            {
                dayText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorDisabledBrush");
                dayText.Opacity = 0.4;
            }

            container.Children.Add(dayText);

            if (hasEvent && isCurrentMonth)
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 3,
                    Height = 3,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                container.Children.Add(dot);
            }
        }

        return container;
    }
}