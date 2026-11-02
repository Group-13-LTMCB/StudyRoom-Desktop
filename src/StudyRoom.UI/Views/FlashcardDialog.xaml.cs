using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using StudyRoom.UI.ViewModels;

namespace StudyRoom.UI.Views;

public partial class FlashcardDialog : Window
{
    private FlashcardViewModel? _viewModel;

    public FlashcardDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public FlashcardDialog(FlashcardViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFlipAnimation -= OnRequestFlipAnimation;
            _viewModel.RequestResetCard -= OnRequestResetCard;
            _viewModel.RequestShow -= OnRequestShow;
            _viewModel.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is FlashcardViewModel vm)
        {
            _viewModel = vm;
            _viewModel.RequestFlipAnimation += OnRequestFlipAnimation;
            _viewModel.RequestResetCard += OnRequestResetCard;
            _viewModel.RequestShow += OnRequestShow;
            _viewModel.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestShow()
    {
        Dispatcher.Invoke(() =>
        {
            if (!IsVisible)
            {
                Show();
            }
            Activate();
        });
    }

    private void OnRequestResetCard()
    {
        Dispatcher.Invoke(() =>
        {
            if (Resources["FlipToBackStoryboard"] is Storyboard flipToBack)
            {
                flipToBack.Stop(this);
            }
            if (Resources["FlipToFrontStoryboard"] is Storyboard flipToFront)
            {
                flipToFront.Stop(this);
            }

            CardScale.ScaleX = 1.0;
            FrontFace.Visibility = Visibility.Visible;
            BackFace.Visibility = Visibility.Collapsed;
        });
    }

    private void OnRequestFlipAnimation(bool isFlipped)
    {
        Dispatcher.Invoke(() =>
        {
            if (Resources["FlipToBackStoryboard"] is Storyboard flipToBack)
            {
                flipToBack.Stop(this);
            }
            if (Resources["FlipToFrontStoryboard"] is Storyboard flipToFront)
            {
                flipToFront.Stop(this);
            }

            string storyboardKey = isFlipped ? "FlipToBackStoryboard" : "FlipToFrontStoryboard";
            if (Resources[storyboardKey] is Storyboard storyboard)
            {
                storyboard.Begin(this);
            }
        });
    }

    private void OnRequestClose()
    {
        Dispatcher.Invoke(Close);
    }

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestFlipAnimation -= OnRequestFlipAnimation;
            _viewModel.RequestResetCard -= OnRequestResetCard;
            _viewModel.RequestShow -= OnRequestShow;
            _viewModel.RequestClose -= OnRequestClose;
        }

        base.OnClosed(e);
    }
}
