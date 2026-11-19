using System;
using CozyFocus.Core;
using CozyFocus.Models;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class FocusTimerController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _timerWidget;
    private Label _timerTagLabel;
    private Label _timerLabel;
    private VisualElement _timerProgressRing;
    private TextField _sessionTagInput;

    private float _totalDurationSeconds;
    private float _currentProgress = 1f;

    private static readonly Color TrackRingColor = new Color(1f, 0.97f, 0.88f, 0.16f);
    private static readonly Color ActiveRingColor = new Color(0.40f, 0.73f, 0.42f, 1f); // #66BB6A

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void Start()
    {
        VisualElement root = _uiDocument.rootVisualElement;
        if (root == null) return;

        _timerWidget = root.Q<VisualElement>("TimerWidget");
        _timerTagLabel = root.Q<Label>("TimerTagLabel");
        _timerLabel = root.Q<Label>("TimerLabel");
        _timerProgressRing = root.Q<VisualElement>("TimerProgressRing");
        _sessionTagInput = root.Q<TextField>("SessionTagInput");

        if (_timerProgressRing != null)
            _timerProgressRing.generateVisualContent += OnGenerateProgressRing;

        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.OnSessionStarted += HandleSessionStarted;
            FocusSessionManager.Instance.OnTimerTick += HandleTimerTick;
            FocusSessionManager.Instance.OnRewardClaimed += HandleRewardClaimed;

            if (FocusSessionManager.Instance.IsSessionActive)
            {
                _totalDurationSeconds = Mathf.Max(1f, FocusSessionManager.Instance.DurationMinutes * 60f);
                ShowTimer();
                HandleTimerTick(FocusSessionManager.Instance.RemainingSeconds);
            }
            else
            {
                HideTimer();
            }
        }
        else
        {
            HideTimer();
        }
    }

    private void OnDestroy()
    {
        if (_timerProgressRing != null)
            _timerProgressRing.generateVisualContent -= OnGenerateProgressRing;

        if (FocusSessionManager.Instance != null)
        {
            FocusSessionManager.Instance.OnSessionStarted -= HandleSessionStarted;
            FocusSessionManager.Instance.OnTimerTick -= HandleTimerTick;
            FocusSessionManager.Instance.OnRewardClaimed -= HandleRewardClaimed;
        }
    }

    private void HandleSessionStarted(string sessionId, int durationMinutes, DateTime startUtc, DateTime endUtc)
    {
        _totalDurationSeconds = Mathf.Max(1f, durationMinutes * 60f);

        if (_timerTagLabel != null)
        {
            string tagText = (_sessionTagInput != null && !string.IsNullOrWhiteSpace(_sessionTagInput.value))
                ? _sessionTagInput.value.Trim()
                : "Study";
            _timerTagLabel.text = tagText;
        }

        ShowTimer();
        HandleTimerTick(_totalDurationSeconds);
    }

    private void HandleTimerTick(float remainingSeconds)
    {
        if (_timerLabel != null)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _timerLabel.text = $"{minutes:00}:{seconds:00}";
        }

        _currentProgress = _totalDurationSeconds > 0f
            ? Mathf.Clamp01(remainingSeconds / _totalDurationSeconds)
            : 0f;

        _timerProgressRing?.MarkDirtyRepaint();
    }

    private void HandleRewardClaimed(ClaimRewardResult result)
    {
        HideTimer();
    }

    private void OnGenerateProgressRing(MeshGenerationContext mgc)
    {
        Rect rect = _timerProgressRing.contentRect;
        if (rect.width <= 4f || rect.height <= 4f) return;

        const float strokeWidth = 4f;
        const float borderRadius = 20f;
        float inset = strokeWidth * 0.5f;

        float left = inset;
        float top = inset;
        float right = rect.width - inset;
        float bottom = rect.height - inset;
        float w = right - left;
        float h = bottom - top;
        float r = Mathf.Clamp(borderRadius - inset, 0f, Mathf.Min(w, h) * 0.5f);

        var painter = mgc.painter2D;
        painter.lineWidth = strokeWidth;
        painter.lineCap = LineCap.Round;

        // 1. Vẽ viền nền mờ toàn chu vi
        painter.strokeColor = TrackRingColor;
        StrokeRoundedRectProgress(painter, left, top, right, bottom, r, 1f);

        // 2. Vẽ viền tiến trình xanh lá theo % thời gian còn lại
        if (_currentProgress > 0.001f)
        {
            painter.strokeColor = ActiveRingColor;
            StrokeRoundedRectProgress(painter, left, top, right, bottom, r, _currentProgress);
        }
    }

    private void StrokeRoundedRectProgress(
        Painter2D painter, float left, float top, float right, float bottom, float r, float progress)
    {
        float w = right - left;
        float h = bottom - top;
        float cx = left + w * 0.5f;

        float halfTop = Mathf.Max(0f, (w * 0.5f) - r);
        float verticalLen = Mathf.Max(0f, h - 2f * r);
        float bottomLen = Mathf.Max(0f, w - 2f * r);
        float arcLen = 0.5f * Mathf.PI * r;

        float totalPerimeter = (2f * halfTop) + (2f * verticalLen) + bottomLen + (4f * arcLen);
        float remaining = totalPerimeter * Mathf.Clamp01(progress);
        if (remaining <= 0f) return;

        painter.BeginPath();
        painter.MoveTo(new Vector2(cx, top));

        // 1. Nửa cạnh trên bên phải
        if (!DrawLineStep(painter, new Vector2(cx, top), new Vector2(right - r, top), halfTop, ref remaining))
        { painter.Stroke(); return; }

        // 2. Góc bo trên-phải
        if (!DrawArcStep(painter, new Vector2(right - r, top + r), r, -90f, 90f, arcLen, ref remaining))
        { painter.Stroke(); return; }

        // 3. Cạnh phải
        if (!DrawLineStep(painter, new Vector2(right, top + r), new Vector2(right, bottom - r), verticalLen, ref remaining))
        { painter.Stroke(); return; }

        // 4. Góc bo dưới-phải
        if (!DrawArcStep(painter, new Vector2(right - r, bottom - r), r, 0f, 90f, arcLen, ref remaining))
        { painter.Stroke(); return; }

        // 5. Cạnh đáy
        if (!DrawLineStep(painter, new Vector2(right - r, bottom), new Vector2(left + r, bottom), bottomLen, ref remaining))
        { painter.Stroke(); return; }

        // 6. Góc bo dưới-trái
        if (!DrawArcStep(painter, new Vector2(left + r, bottom - r), r, 90f, 90f, arcLen, ref remaining))
        { painter.Stroke(); return; }

        // 7. Cạnh trái
        if (!DrawLineStep(painter, new Vector2(left, bottom - r), new Vector2(left, top + r), verticalLen, ref remaining))
        { painter.Stroke(); return; }

        // 8. Góc bo trên-trái
        if (!DrawArcStep(painter, new Vector2(left + r, top + r), r, 180f, 90f, arcLen, ref remaining))
        { painter.Stroke(); return; }

        // 9. Nửa cạnh trên bên trái về lại đỉnh giữa
        DrawLineStep(painter, new Vector2(left + r, top), new Vector2(cx, top), halfTop, ref remaining);
        painter.Stroke();
    }

    private bool DrawLineStep(Painter2D painter, Vector2 from, Vector2 to, float segLen, ref float remaining)
    {
        if (segLen <= 0.0001f) return true;

        if (remaining >= segLen)
        {
            painter.LineTo(to);
            remaining -= segLen;
            return true;
        }

        float t = remaining / segLen;
        painter.LineTo(Vector2.Lerp(from, to, t));
        remaining = 0f;
        return false;
    }

    private bool DrawArcStep(Painter2D painter, Vector2 center, float r, float startDeg, float sweepDeg, float arcLen, ref float remaining)
    {
        if (arcLen <= 0.0001f) return true;

        if (remaining >= arcLen)
        {
            painter.Arc(center, r, Angle.Degrees(startDeg), Angle.Degrees(startDeg + sweepDeg), ArcDirection.Clockwise);
            remaining -= arcLen;
            return true;
        }

        float t = remaining / arcLen;
        painter.Arc(center, r, Angle.Degrees(startDeg), Angle.Degrees(startDeg + sweepDeg * t), ArcDirection.Clockwise);
        remaining = 0f;
        return false;
    }

    private void ShowTimer() => _timerWidget?.RemoveFromClassList("hidden");
    private void HideTimer() => _timerWidget?.AddToClassList("hidden");
}