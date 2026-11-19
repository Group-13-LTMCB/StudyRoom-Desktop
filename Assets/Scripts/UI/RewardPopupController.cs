using System;
using CozyFocus.Core;
using CozyFocus.Models;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class RewardPopupController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _completionModal;
    private Label _earnedPointsLabel;
    private Button _continueButton;

    public event Action OnFlowCompleted;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        VisualElement root = _uiDocument.rootVisualElement;
        if (root == null) return;

        _completionModal = root.Q<VisualElement>("CompletionModal");
        _earnedPointsLabel = root.Q<Label>("EarnedPointsLabel");
        _continueButton = root.Q<Button>("ContinueButton");

        Hide();

        if (_continueButton != null)
            _continueButton.clicked += HandleContinueClicked;

        if (FocusSessionManager.Instance != null)
            FocusSessionManager.Instance.OnRewardClaimed += HandleRewardClaimed;
    }

    private void OnDestroy()
    {
        if (_continueButton != null)
            _continueButton.clicked -= HandleContinueClicked;

        if (FocusSessionManager.Instance != null)
            FocusSessionManager.Instance.OnRewardClaimed -= HandleRewardClaimed;
    }

    private void HandleRewardClaimed(ClaimRewardResult result)
    {
        if (result == null) return;

        if (_earnedPointsLabel != null)
            _earnedPointsLabel.text = $"+{result.PointsEarned} Điểm rèn luyện";

        _completionModal?.RemoveFromClassList("hidden");
    }

    private void HandleContinueClicked()
    {
        Hide();
        OnFlowCompleted?.Invoke();
    }

    private void Hide()
    {
        _completionModal?.AddToClassList("hidden");
    }
}