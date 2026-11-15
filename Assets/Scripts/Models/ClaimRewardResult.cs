#nullable enable
using System;

namespace CozyFocus.Models
{
    /// <summary>
    /// DTO đại diện cho kết quả trả về từ RPC claim_focus_reward.
    /// Sử dụng trường chuẩn tương thích trực tiếp với Unity JsonUtility mà không phụ thuộc thư viện ngoài.
    /// </summary>
    [Serializable]
    public class ClaimRewardResult
    {
        public bool success;
        public int points_earned;
        public int new_total_points;

        public bool Success
        {
            get => success;
            set => success = value;
        }

        public int PointsEarned
        {
            get => points_earned;
            set => points_earned = value;
        }

        public int NewTotalPoints
        {
            get => new_total_points;
            set => new_total_points = value;
        }
    }
}

