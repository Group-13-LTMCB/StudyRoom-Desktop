using System.Windows;

namespace StudyRoom.UI.Helpers;

/// <summary>
/// Attached properties dùng cho style ô nhập của màn Auth (placeholder, icon, hiện/ẩn mật khẩu).
/// PasswordBox không hỗ trợ binding Text thông thường; Monitor cập nhật trạng thái placeholder,
/// còn PlainText chỉ được giữ khi IsRevealed bật. Không thay thế handler PasswordChanged của ViewModel.
/// </summary>
public static class InputAssist
{
    // ----- Placeholder -----
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(InputAssist),
            new PropertyMetadata(string.Empty));

    public static string GetPlaceholder(DependencyObject d) => (string)d.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject d, string value) => d.SetValue(PlaceholderProperty, value);

    // ----- Icon bên trái ô nhập -----
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached("Icon", typeof(Wpf.Ui.Controls.SymbolRegular), typeof(InputAssist),
            new PropertyMetadata(default(Wpf.Ui.Controls.SymbolRegular)));

    public static Wpf.Ui.Controls.SymbolRegular GetIcon(DependencyObject d) =>
        (Wpf.Ui.Controls.SymbolRegular)d.GetValue(IconProperty);
    public static void SetIcon(DependencyObject d, Wpf.Ui.Controls.SymbolRegular value) =>
        d.SetValue(IconProperty, value);

    // ----- PasswordBox: theo dõi nội dung -----
    public static readonly DependencyProperty MonitorProperty =
        DependencyProperty.RegisterAttached("Monitor", typeof(bool), typeof(InputAssist),
            new PropertyMetadata(false, OnMonitorChanged));

    public static bool GetMonitor(DependencyObject d) => (bool)d.GetValue(MonitorProperty);
    public static void SetMonitor(DependencyObject d, bool value) => d.SetValue(MonitorProperty, value);

    public static readonly DependencyProperty HasPasswordProperty =
        DependencyProperty.RegisterAttached("HasPassword", typeof(bool), typeof(InputAssist),
            new PropertyMetadata(false));

    public static bool GetHasPassword(DependencyObject d) => (bool)d.GetValue(HasPasswordProperty);
    public static void SetHasPassword(DependencyObject d, bool value) => d.SetValue(HasPasswordProperty, value);

    // ----- PasswordBox: hiện mật khẩu dạng chữ (chỉ lưu khi đang bật) -----
    public static readonly DependencyProperty IsRevealedProperty =
        DependencyProperty.RegisterAttached("IsRevealed", typeof(bool), typeof(InputAssist),
            new PropertyMetadata(false, OnIsRevealedChanged));

    public static bool GetIsRevealed(DependencyObject d) => (bool)d.GetValue(IsRevealedProperty);
    public static void SetIsRevealed(DependencyObject d, bool value) => d.SetValue(IsRevealedProperty, value);

    public static readonly DependencyProperty PlainTextProperty =
        DependencyProperty.RegisterAttached("PlainText", typeof(string), typeof(InputAssist),
            new PropertyMetadata(string.Empty));

    public static string GetPlainText(DependencyObject d) => (string)d.GetValue(PlainTextProperty);
    public static void SetPlainText(DependencyObject d, string value) => d.SetValue(PlainTextProperty, value);

    private static void OnMonitorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.PasswordBox box) return;

        box.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue)
        {
            box.PasswordChanged += OnPasswordChanged;
            Sync(box);
        }
    }

    private static void OnIsRevealedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is System.Windows.Controls.PasswordBox box)
            SetPlainText(box, (bool)e.NewValue ? box.Password : string.Empty);
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox box) Sync(box);
    }

    private static void Sync(System.Windows.Controls.PasswordBox box)
    {
        SetHasPassword(box, box.Password.Length > 0);
        if (GetIsRevealed(box)) SetPlainText(box, box.Password);
    }
}
