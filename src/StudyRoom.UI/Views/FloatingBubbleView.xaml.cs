using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StudyRoom.Application.Engines;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI.Views;

public partial class FloatingBubbleView : Window
{
    private Point _mouseDownScreenPos;
    private bool _isDragging;
    private bool _isExplicitClose;

    public FloatingBubbleViewModel ViewModel => (FloatingBubbleViewModel)DataContext;

    public FloatingBubbleView(FloatingBubbleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += OnRequestClose;
        viewModel.RequestToggleVisibility += OnRequestToggleVisibility;
        viewModel.RequestFocusInput += FocusInput;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        IsVisibleChanged += FloatingBubbleView_IsVisibleChanged;

        if (viewModel.Messages is INotifyCollectionChanged notifyCollection)
        {
            notifyCollection.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    public FloatingBubbleView() : this(new FloatingBubbleViewModel(new TokenBucketRateLimiter(5, 1)))
    {
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        PositionBottomRight();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        // Position at bottom-right corner as a collapsed floating bubble (approx 92x92 with margins)
        Left = Math.Max(workArea.Left, workArea.Right - 110);
        Top = Math.Max(workArea.Top, workArea.Bottom - 130);
    }

    private void AdjustPositionForExpandedState(bool isExpanded)
    {
        var workArea = SystemParameters.WorkArea;
        if (isExpanded)
        {
            // Expand upward and leftward if near screen boundaries so content remains visible
            const double expandedWidth = 392;
            const double expandedHeight = 512;
            if (Left + expandedWidth > workArea.Right)
            {
                Left = Math.Max(workArea.Left, workArea.Right - expandedWidth);
            }
            if (Top + expandedHeight > workArea.Bottom)
            {
                Top = Math.Max(workArea.Top, workArea.Bottom - expandedHeight);
            }
        }
    }

    private void ClampToScreen()
    {
        double minX = SystemParameters.VirtualScreenLeft;
        double maxX = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60;
        double minY = SystemParameters.VirtualScreenTop;
        double maxY = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60;

        if (Left < minX) Left = minX;
        else if (Left > maxX) Left = maxX;

        if (Top < minY) Top = minY;
        else if (Top > maxY) Top = maxY;
    }

    public void FocusInput()
    {
        Dispatcher.BeginInvoke(() =>
        {
            MessageInputTextBox?.Focus();
            MessageInputTextBox?.SelectAll();
        });
    }

    public void ForceClose()
    {
        _isExplicitClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        IsVisibleChanged -= FloatingBubbleView_IsVisibleChanged;

        if (DataContext is FloatingBubbleViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestToggleVisibility -= OnRequestToggleVisibility;
            vm.RequestFocusInput -= FocusInput;
            vm.PropertyChanged -= ViewModel_PropertyChanged;

            if (vm.Messages is INotifyCollectionChanged notifyCollection)
            {
                notifyCollection.CollectionChanged -= OnMessagesCollectionChanged;
            }
        }

        base.OnClosing(e);
    }

    private void OnRequestClose()
    {
        Hide();
    }

    private void OnRequestToggleVisibility()
    {
        if (ViewModel.IsVisible)
        {
            Show();
            Activate();
            if (ViewModel.IsExpanded)
            {
                FocusInput();
            }
        }
        else
        {
            Hide();
        }
    }

    private void FloatingBubbleView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is FloatingBubbleViewModel vm && vm.IsVisible != IsVisible)
        {
            vm.IsVisible = IsVisible;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FloatingBubbleViewModel.IsExpanded))
        {
            Dispatcher.BeginInvoke(() => AdjustPositionForExpandedState(ViewModel.IsExpanded));
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            ClampToScreen();
        }
    }

    private void Bubble_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownScreenPos = PointToScreen(e.GetPosition(this));
        _isDragging = false;
    }

    private void Bubble_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            ViewModel.ToggleExpand();
        }
        _isDragging = false;
    }

    private void Bubble_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            Point currentScreenPos = PointToScreen(e.GetPosition(this));
            Vector diff = currentScreenPos - _mouseDownScreenPos;
            if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
            {
                _isDragging = true;
                DragMove();
                ClampToScreen();
            }
        }
    }

    private void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            ViewModel.SendMessageCommand.Execute(null);
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Reset)
        {
            Dispatcher.BeginInvoke(() => ChatScrollViewer?.ScrollToEnd());
        }
    }
}
