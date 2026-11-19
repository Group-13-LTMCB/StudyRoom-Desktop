using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CozyFocus.Models;
using Supabase;
using UnityEngine;

namespace CozyFocus.Core
{
    public class FocusSessionService
    {
        private const string RpcStartFocusSession = "start_focus_session";
        private const string RpcClaimFocusReward = "claim_focus_reward";
        private const string RpcGetTrainingPoints = "get_training_points";

        private static Func<Client> _clientProvider;
        private readonly Client _client;

        [Serializable]
        private class SessionIdPayload
        {
            public string session_id;
            public string id;
            public string p_session_id;
        }

        [Serializable]
        private class TrainingPointsPayload
        {
            public int training_points;
            public int total_points;
            public int points;
        }

        public static void RegisterClientProvider(Func<Client> provider)
        {
            _clientProvider = provider;
        }

        public FocusSessionService()
        {
            _client = _clientProvider?.Invoke();
            if (_client == null)
            {
                throw new InvalidOperationException(
                    "Supabase Client chưa sẵn sàng. Hãy đảm bảo SupabaseSessionConnector đã gắn trên [Services] và SupabaseManager đã khởi tạo.");
            }
        }

        public FocusSessionService(Client client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<string> StartFocusSessionAsync(int durationMinutes, string roomType)
        {
            if (durationMinutes < 1 || durationMinutes > 90)
                throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Thời lượng phiên học phải từ 1 đến 90 phút.");

            if (string.IsNullOrWhiteSpace(roomType))
                throw new ArgumentException("Loại phòng (p_room_type) không được để trống.", nameof(roomType));

            var parameters = new Dictionary<string, object>
            {
                { "p_duration_minutes", durationMinutes },
                { "p_room_type", roomType }
            };

            var response = await _client.Rpc(RpcStartFocusSession, parameters);
            string content = response?.Content;

            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("RPC start_focus_session trả về dữ liệu rỗng.");

            string sessionId = ParseSessionId(content);
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new Exception($"Không thể trích xuất session_id từ phản hồi: {content}");

            return sessionId;
        }

        public async Task<ClaimRewardResult> ClaimFocusRewardAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("session_id không hợp lệ.", nameof(sessionId));

            var parameters = new Dictionary<string, object>
            {
                { "p_session_id", sessionId }
            };

            var response = await _client.Rpc(RpcClaimFocusReward, parameters);
            string content = response?.Content;

            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("RPC claim_focus_reward trả về dữ liệu rỗng.");

            return ParseClaimRewardResult(content);
        }

        public async Task<int> GetCurrentTrainingPointsAsync()
        {
            // Bỏ qua gọi server nếu người dùng chưa đăng nhập (tránh cảnh báo khi test trực tiếp SampleScene)
            if (_client.Auth?.CurrentUser == null)
                return 0;

            var response = await _client.Rpc(RpcGetTrainingPoints, null);
            string content = response?.Content;

            if (string.IsNullOrWhiteSpace(content))
                return 0;

            string trimmed = content.Trim();
            if (int.TryParse(trimmed, out int scalarPoints))
                return scalarPoints;

            string objectJson = UnwrapSingleObjectJson(trimmed);
            if (string.IsNullOrWhiteSpace(objectJson))
                return 0;

            var payload = JsonUtility.FromJson<TrainingPointsPayload>(objectJson);
            if (payload == null) return 0;

            if (payload.training_points != 0) return payload.training_points;
            if (payload.total_points != 0) return payload.total_points;
            return payload.points;
        }

        private string ParseSessionId(string rawJson)
        {
            string trimmed = rawJson.Trim();

            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            string objectJson = UnwrapSingleObjectJson(trimmed);
            if (string.IsNullOrWhiteSpace(objectJson))
                return null;

            SessionIdPayload payload = JsonUtility.FromJson<SessionIdPayload>(objectJson);
            if (payload == null)
                return null;

            if (!string.IsNullOrWhiteSpace(payload.session_id)) return payload.session_id;
            if (!string.IsNullOrWhiteSpace(payload.id)) return payload.id;
            if (!string.IsNullOrWhiteSpace(payload.p_session_id)) return payload.p_session_id;

            return null;
        }

        private ClaimRewardResult ParseClaimRewardResult(string rawJson)
        {
            string objectJson = UnwrapSingleObjectJson(rawJson.Trim());
            if (string.IsNullOrWhiteSpace(objectJson))
                throw new Exception($"Định dạng phản hồi claim_focus_reward không hợp lệ: {rawJson}");

            ClaimRewardResult result = JsonUtility.FromJson<ClaimRewardResult>(objectJson);
            if (result == null)
                throw new Exception($"Không thể đọc dữ liệu từ claim_focus_reward: {rawJson}");

            if (!result.Success)
                throw new Exception("Máy chủ từ chối xác nhận phần thưởng cho phiên học này.");

            return result;
        }

        private string UnwrapSingleObjectJson(string trimmed)
        {
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                int firstBrace = trimmed.IndexOf('{');
                int lastBrace = trimmed.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
                }
                return null;
            }

            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return trimmed;
            }

            return null;
        }
    }
}