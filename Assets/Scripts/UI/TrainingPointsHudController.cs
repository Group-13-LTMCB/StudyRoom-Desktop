using CozyFocus.Core;
using CozyFocus.Models;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TrainingPointsHudController : MonoBehaviour
{
    [SerializeField] private int initialPoints = 0;

    private UIDocument _uiDocument;
    private VisualElement _trainingPointsHud;
    private Label _trainingPointsLabel;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        VisualElement root = _uiDocument.rootVisualElement;
        if (root == null) return;

        _trainingPointsHud = root.Q<VisualElement>("TrainingPointsHUD");
        _trainingPointsLabel = root.Q<Label>("TrainingPointsLabel");

        SetPointsDisplay(initialPoints, playPunchAnimation: false);

        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.OnInitialPointsLoaded += HandleInitialPointsLoaded;
            FocusSessionManager.Instance.OnRewardClaimed += HandleRewardClaimed;
            FocusSessionManager.Instance.LoadInitialTrainingPoints();
        }
    }

    private void OnDestroy()
    {
        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.OnInitialPointsLoaded -= HandleInitialPointsLoaded;
            FocusSessionManager.Instance.OnRewardClaimed -= HandleRewardClaimed;
        }
    }

    /// <summary>
    /// Nạp tổng điểm ban đầu từ profile server (không chạy hiệu ứng nảy).
    /// </summary>
    public void SetInitialPoints(int serverTotalPoints)
    {
        initialPoints = serverTotalPoints;
        SetPointsDisplay(serverTotalPoints, playPunchAnimation: false);
    }

    private void HandleInitialPointsLoaded(int points)
    {
        SetInitialPoints(points);
    }

    private void HandleRewardClaimed(ClaimRewardResult result)
    {
        if (result == null) return;

        // Dùng trực tiếp tổng điểm mới do server trả về
        SetPointsDisplay(result.NewTotalPoints, playPunchAnimation: true);
    }

    private void SetPointsDisplay(int totalPoints, bool playPunchAnimation)
    {
        if (_trainingPointsLabel != null)
            _trainingPointsLabel.text = totalPoints.ToString();

        if (playPunchAnimation && _trainingPointsHud != null)
        {
            _trainingPointsHud.AddToClassList("wood-badge--punch");
            _trainingPointsHud.schedule.Execute(() =>
            {
                _trainingPointsHud.RemoveFromClassList("wood-badge--punch");
            }).ExecuteLater(180);
        }
    }
}