#nullable enable
using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CozyFocus.Models
{
    /// <summary>
    /// DTO đại diện cho bảng public.profiles trong Supabase.
    /// Lưu trữ thông tin sinh viên, điểm rèn luyện và avatar.
    /// </summary>
    [Table("profiles")]
    public class ProfileModel : BaseModel
    {
        [PrimaryKey("id", false)]
        public string Id { get; set; } = string.Empty;

        [Column("student_name")]
        public string StudentName { get; set; } = "Sinh viên VNU";

        [Column("training_points")]
        public int TrainingPoints { get; set; } = 0;

        [Column("avatar_id")]
        public string AvatarId { get; set; } = "default_student";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
