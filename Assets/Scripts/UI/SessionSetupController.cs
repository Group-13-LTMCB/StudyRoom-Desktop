using System;
using CozyFocus.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SessionSetupController : MonoBehaviour
{
    [Header("Tùy chọn ghi đè loại phòng (để trống để tự nhận diện theo tên Scene)")]
    [SerializeField] private string overrideRoomType = "";

    private UIDocument _uiDocument;
    private VisualElement _sessionSetupModal;
    private SliderInt _durationSlider;
    private Label _durationValueLabel;
    private Label _estimatedRewardLabel;
    private TextField _sessionTagInput;
    private Button _startSessionButton;
    private Button _closeSetupButton;
    private Button _soundToggleButton;
    private VisualElement _errorBanner;
    private Label _errorMessageLabel;

    private bool _isSoundMuted;

    public string CurrentSessionTag =>
        (_sessionTagInput != null && !string.IsNullOrWhiteSpace(_sessionTagInput.value))
            ? _sessionTagInput.value.Trim()
            : "Study";

    public event Action<bool> OnSoundToggled;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        VisualElement root = _uiDocument.rootVisualElement;
        if (root == null) return;

        _sessionSetupModal = root.Q<VisualElement>("SessionSetupModal");
        _durationSlider = root.Q<SliderInt>("DurationSlider");
        _durationValueLabel = root.Q<Label>("DurationValueLabel");
        _estimatedRewardLabel = root.Q<Label>("EstimatedRewardLabel");
        _sessionTagInput = root.Q<TextField>("SessionTagInput");
        _startSessionButton = root.Q<Button>("StartSessionButton");
        _closeSetupButton = root.Q<Button>("CloseSetupButton");
        _soundToggleButton = root.Q<Button>("SoundToggleButton");
        _errorBanner = root.Q<VisualElement>("ErrorBanner");
        _errorMessageLabel = root.Q<Label>("ErrorMessageLabel");

        _durationSlider?.RegisterValueChangedCallback(OnSliderValueChanged);
        RegisterPresetButton(root, "Preset15Button", 15);
        RegisterPresetButton(root, "Preset25Button", 25);
        RegisterPresetButton(root, "Preset45Button", 45);
        RegisterPresetButton(root, "Preset60Button", 60);
        RegisterPresetButton(root, "Preset90Button", 90);

        if (_startSessionButton != null)
            _startSessionButton.clicked += HandleStartClicked;
        if (_closeSetupButton != null)
            _closeSetupButton.clicked += Hide;
        if (_soundToggleButton != null)
            _soundToggleButton.clicked += HandleSoundToggled;

        int initialMinutes = _durationSlider != null ? _durationSlider.value : 25;
        RefreshLabels(initialMinutes);

        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.OnSessionStartPending += HandleStartPending;
            FocusSessionManager.Instance.OnSessionStarted += HandleSessionStarted;
            FocusSessionManager.Instance.OnSessionError += HandleSessionError;
        }
    }

    private void OnDestroy()
    {
        _durationSlider?.UnregisterValueChangedCallback(OnSliderValueChanged);
        if (_startSessionButton != null)
            _startSessionButton.clicked -= HandleStartClicked;
        if (_closeSetupButton != null)
            _closeSetupButton.clicked -= Hide;
        if (_soundToggleButton != null)
            _soundToggleButton.clicked -= HandleSoundToggled;

        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.OnSessionStartPending -= HandleStartPending;
            FocusSessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
            FocusSessionManager.Instance.OnSessionError -= HandleSessionError;
        }
    }

    private void RegisterPresetButton(VisualElement root, string buttonName, int minutes)
    {
        Button btn = root.Q<Button>(buttonName);
        if (btn != null && _durationSlider != null)
            btn.clicked += () => _durationSlider.value = minutes;
    }

    private void OnSliderValueChanged(ChangeEvent<int> evt)
    {
        RefreshLabels(evt.newValue);
    }

    private void RefreshLabels(int minutes)
    {
        if (_durationValueLabel != null)
            _durationValueLabel.text = $"{minutes}m";

        if (_estimatedRewardLabel != null)
            _estimatedRewardLabel.text = $"Total Reward: +{minutes} Điểm rèn luyện";
    }

    private void HandleStartClicked()
    {
        int selectedMinutes = _durationSlider != null ? _durationSlider.value : 25;
        string roomType = ResolveRoomType();

        HideError();

        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.StartSession(selectedMinutes, roomType);
        }
        else
        {
            ShowError("Không tìm thấy FocusSessionManager trên [Services].");
        }
    }

    private string ResolveRoomType()
    {
        if (!string.IsNullOrWhiteSpace(overrideRoomType))
            return overrideRoomType.Trim();

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(activeSceneName) &&
            activeSceneName.IndexOf("library", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "library";
        }

        return "dorm";
    }

    private void HandleStartPending()
    {
        if (_startSessionButton != null)
        {
            _startSessionButton.SetEnabled(false);
            _startSessionButton.text = "Đang kết nối...";
        }
    }

    private void HandleSessionStarted(string sessionId, int durationMinutes, DateTime startUtc, DateTime endUtc)
    {
        ResetStartButton();
        HideError();
        Hide();
    }

    private void HandleSessionError(string errorMessage)
    {
        ResetStartButton();
        ShowError(errorMessage);
    }

    private void ResetStartButton()
    {
        if (_startSessionButton != null)
        {
            _startSessionButton.SetEnabled(true);
            _startSessionButton.text = "Start a session";
        }
    }

    private void HandleSoundToggled()
    {
        _isSoundMuted = !_isSoundMuted;
        if (_soundToggleButton != null)
            _soundToggleButton.text = _isSoundMuted ? "🔇" : "🔊";

        OnSoundToggled?.Invoke(_isSoundMuted);
    }

    public void Show()
    {
        ResetStartButton();
        HideError();
        _sessionSetupModal?.RemoveFromClassList("hidden");
    }

    public void Hide()
    {
        _sessionSetupModal?.AddToClassList("hidden");
    }

    private void ShowError(string message)
    {
        if (_errorMessageLabel != null)
            _errorMessageLabel.text = message;

        _errorBanner?.RemoveFromClassList("hidden");
    }

    private void HideError()
    {
        _errorBanner?.AddToClassList("hidden");
    }
    public bool IsOpen => _sessionSetupModal != null && !_sessionSetupModal.ClassListContains("hidden");
}