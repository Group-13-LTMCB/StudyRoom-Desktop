#nullable enable
using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CozyFocus.Models
{
    /// <summary>
    /// DTO đại diện cho bảng public.focus_sessions trong Supabase.
    /// Lưu trữ lịch sử và trạng thái phiên học tập Pomodoro.
    /// </summary>
    [Table("focus_sessions")]
    public class FocusSessionModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        [Column("duration_minutes")]
        public int DurationMinutes { get; set; } = 25;

        [Column("room_type")]
        public string RoomType { get; set; } = "dorm";

        [Column("started_at")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("status")]
        public string Status { get; set; } = "in_progress";

        [Column("reward_points")]
        public int RewardPoints { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
