using System;
using System.Threading.Tasks;
using CozyFocus.Models;
using Supabase;
using UnityEngine;

namespace CozyFocus.Core
{
    public class FocusSessionManager : MonoBehaviour
    {
        public static FocusSessionManager Instance { get; private set; }

        [Header("Cấu hình phòng mặc định")]
        [SerializeField] private string currentRoomType = "dorm"; // "dorm" hoặc "library"

        private FocusSessionService _service;
        private bool _isRequestInFlight;

        // --- Trạng thái phiên học hiện tại ---
        public string CurrentSessionId { get; private set; }
        public int DurationMinutes { get; private set; }
        public string CurrentRoomType => currentRoomType;
        public DateTime StartTimeUtc { get; private set; }
        public DateTime EndTimeUtc { get; private set; }
        public bool IsSessionActive { get; private set; }

        public float RemainingSeconds
        {
            get
            {
                if (!IsSessionActive) return 0f;
                double remaining = (EndTimeUtc - DateTime.UtcNow).TotalSeconds;
                return Mathf.Max(0f, (float)remaining);
            }
        }

        // --- Các sự kiện điều phối cho UI và hệ thống khác ---
        public event Action OnSessionStartPending;
        public event Action<string, int, DateTime, DateTime> OnSessionStarted;
        public event Action<float> OnTimerTick;
        public event Action<ClaimRewardResult> OnRewardClaimed;
        public event Action<string> OnSessionError;
        public event Action<int> OnInitialPointsLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }

        private void Update()
        {
            if (!IsSessionActive) return;

            float remaining = RemainingSeconds;
            OnTimerTick?.Invoke(remaining);

            if (remaining <= 0f)
            {
                IsSessionActive = false;
                _ = ClaimRewardInternalAsync();
            }
        }

        public void Initialize(Client client)
        {
            _service = new FocusSessionService(client);
        }

        public void SetRoomType(string roomType)
        {
            if (!string.IsNullOrWhiteSpace(roomType))
            {
                currentRoomType = roomType.Trim();
            }
        }

        public async void LoadInitialTrainingPoints()
        {
            try
            {
                EnsureServiceInitialized();
                int points = await _service.GetCurrentTrainingPointsAsync();
                OnInitialPointsLoaded?.Invoke(points);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FocusSessionManager] Chưa tải được điểm ban đầu từ profile: {ex.Message}");
            }
        }

        public async void StartSession(int durationMinutes, string overrideRoomType = null)
        {
            if (IsSessionActive || _isRequestInFlight) return;

            string targetRoom = string.IsNullOrWhiteSpace(overrideRoomType) ? currentRoomType : overrideRoomType;

            _isRequestInFlight = true;
            OnSessionStartPending?.Invoke();

            try
            {
                EnsureServiceInitialized();
                string sessionId = await _service.StartFocusSessionAsync(durationMinutes, targetRoom);

                CurrentSessionId = sessionId;
                DurationMinutes = durationMinutes;
                currentRoomType = targetRoom;
                StartTimeUtc = DateTime.UtcNow;
                EndTimeUtc = StartTimeUtc.AddMinutes(durationMinutes);
                IsSessionActive = true;

                OnSessionStarted?.Invoke(CurrentSessionId, DurationMinutes, StartTimeUtc, EndTimeUtc);
                OnTimerTick?.Invoke(RemainingSeconds);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FocusSessionManager] Lỗi bắt đầu phiên học: {ex.Message}");
                IsSessionActive = false;
                OnSessionError?.Invoke($"Không thể bắt đầu phiên học: {ex.Message}");
            }
            finally
            {
                _isRequestInFlight = false;
            }
        }

        public async void RetryClaimReward()
        {
            if (IsSessionActive || _isRequestInFlight || string.IsNullOrWhiteSpace(CurrentSessionId))
                return;

            await ClaimRewardInternalAsync();
        }

        public void CancelLocalSession()
        {
            IsSessionActive = false;
            CurrentSessionId = null;
        }

        private async Task ClaimRewardInternalAsync()
        {
            if (_isRequestInFlight || string.IsNullOrWhiteSpace(CurrentSessionId)) return;

            _isRequestInFlight = true;

            try
            {
                EnsureServiceInitialized();
                ClaimRewardResult result = await _service.ClaimFocusRewardAsync(CurrentSessionId);

                CurrentSessionId = null;
                OnRewardClaimed?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FocusSessionManager] Lỗi nhận thưởng RPC: {ex.Message}");
                OnSessionError?.Invoke($"Lỗi xác thực nhận thưởng: {ex.Message}");
            }
            finally
            {
                _isRequestInFlight = false;
            }
        }

        private void EnsureServiceInitialized()
        {
            if (_service == null)
            {
                _service = new FocusSessionService();
            }
        }
    }
}