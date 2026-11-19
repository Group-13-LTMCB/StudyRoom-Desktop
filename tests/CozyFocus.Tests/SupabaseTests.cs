using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Supabase;
using Supabase.Postgrest.Attributes;
using CozyFocus.Models;

namespace CozyFocus.Tests
{
    public class SupabaseTests
    {
        [Fact]
        public void AppConfigJson_ShouldExistAndHaveValidStructure()
        {
            // Tìm file AppConfig.json từ thư mục project
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.GetFullPath(Path.Combine(currentDir, "../../../../../Assets/Resources/Config/AppConfig.json"));

            Assert.True(File.Exists(configPath), $"File AppConfig.json không tồn tại tại: {configPath}");

            string json = File.ReadAllText(configPath);
            Assert.False(string.IsNullOrWhiteSpace(json), "AppConfig.json không được rỗng.");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            bool hasUrl = root.TryGetProperty("supabaseUrl", out var urlProp) || root.TryGetProperty("url", out urlProp);
            bool hasKey = root.TryGetProperty("supabaseAnonKey", out var keyProp) || root.TryGetProperty("anon_key", out keyProp);

            Assert.True(hasUrl, "AppConfig.json phải chứa thuộc tính 'supabaseUrl' hoặc 'url'");
            Assert.True(hasKey, "AppConfig.json phải chứa thuộc tính 'supabaseAnonKey' hoặc 'anon_key'");
            Assert.False(string.IsNullOrWhiteSpace(urlProp.GetString()), "URL không được để trống");
            Assert.False(string.IsNullOrWhiteSpace(keyProp.GetString()), "Anon key không được để trống");
        }

        [Fact]
        public void ProfileModel_ShouldHaveCorrectTableAndColumns()
        {
            var tableAttr = typeof(ProfileModel).GetCustomAttribute<TableAttribute>();
            Assert.NotNull(tableAttr);
            Assert.Equal("profiles", tableAttr.Name);

            var idProp = typeof(ProfileModel).GetProperty("Id");
            Assert.NotNull(idProp);
            var pkAttr = idProp.GetCustomAttribute<PrimaryKeyAttribute>();
            Assert.NotNull(pkAttr);
            Assert.Equal("id", pkAttr.ColumnName);

            var nameProp = typeof(ProfileModel).GetProperty("StudentName");
            Assert.NotNull(nameProp);
            var colNameAttr = nameProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colNameAttr);
            Assert.Equal("student_name", colNameAttr.ColumnName);

            var pointsProp = typeof(ProfileModel).GetProperty("TrainingPoints");
            Assert.NotNull(pointsProp);
            var colPointsAttr = pointsProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colPointsAttr);
            Assert.Equal("training_points", colPointsAttr.ColumnName);

            var avatarProp = typeof(ProfileModel).GetProperty("AvatarId");
            Assert.NotNull(avatarProp);
            var colAvatarAttr = avatarProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colAvatarAttr);
            Assert.Equal("avatar_id", colAvatarAttr.ColumnName);

            var createdProp = typeof(ProfileModel).GetProperty("CreatedAt");
            Assert.NotNull(createdProp);
            var colCreatedAttr = createdProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colCreatedAttr);
            Assert.Equal("created_at", colCreatedAttr.ColumnName);

            var updatedProp = typeof(ProfileModel).GetProperty("UpdatedAt");
            Assert.NotNull(updatedProp);
            var colUpdatedAttr = updatedProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colUpdatedAttr);
            Assert.Equal("updated_at", colUpdatedAttr.ColumnName);
        }

        [Fact]
        public void FocusSessionModel_ShouldHaveCorrectTableAndColumns()
        {
            var tableAttr = typeof(FocusSessionModel).GetCustomAttribute<TableAttribute>();
            Assert.NotNull(tableAttr);
            Assert.Equal("focus_sessions", tableAttr.Name);

            var idProp = typeof(FocusSessionModel).GetProperty("Id");
            Assert.NotNull(idProp);
            var pkAttr = idProp.GetCustomAttribute<PrimaryKeyAttribute>();
            Assert.NotNull(pkAttr);
            Assert.Equal("id", pkAttr.ColumnName);

            var userProp = typeof(FocusSessionModel).GetProperty("UserId");
            Assert.NotNull(userProp);
            var colUserAttr = userProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colUserAttr);
            Assert.Equal("user_id", colUserAttr.ColumnName);

            var durationProp = typeof(FocusSessionModel).GetProperty("DurationMinutes");
            Assert.NotNull(durationProp);
            var colDurationAttr = durationProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colDurationAttr);
            Assert.Equal("duration_minutes", colDurationAttr.ColumnName);

            var roomProp = typeof(FocusSessionModel).GetProperty("RoomType");
            Assert.NotNull(roomProp);
            var colRoomAttr = roomProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colRoomAttr);
            Assert.Equal("room_type", colRoomAttr.ColumnName);

            var statusProp = typeof(FocusSessionModel).GetProperty("Status");
            Assert.NotNull(statusProp);
            var colStatusAttr = statusProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colStatusAttr);
            Assert.Equal("status", colStatusAttr.ColumnName);

            var rewardProp = typeof(FocusSessionModel).GetProperty("RewardPoints");
            Assert.NotNull(rewardProp);
            var colRewardAttr = rewardProp.GetCustomAttribute<ColumnAttribute>();
            Assert.NotNull(colRewardAttr);
            Assert.Equal("reward_points", colRewardAttr.ColumnName);
        }

        [Fact]
        public async Task SupabaseClient_ShouldInitializeAsyncWithoutCrash()
        {
            var options = new SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false
            };

            var client = new Supabase.Client("https://mockproject.supabase.co", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.dummy", options);
            Assert.NotNull(client);

            // Kiểm tra InitializeAsync không ném ngoại lệ gây sập tiến trình
            await client.InitializeAsync();
            Assert.NotNull(client.Auth);
            Assert.NotNull(client.Postgrest);
        }

        [Fact]
        public void ProfileModel_DefaultValues_ShouldMatchSpec()
        {
            var profile = new ProfileModel();
            Assert.Equal("Sinh viên VNU", profile.StudentName);
            Assert.Equal(0, profile.TrainingPoints);
            Assert.Equal("default_student", profile.AvatarId);
        }

        [Fact]
        public void FocusSessionModel_DefaultValues_ShouldMatchSpec()
        {
            var session = new FocusSessionModel();
            Assert.Equal(25, session.DurationMinutes);
            Assert.Equal("dorm", session.RoomType);
            Assert.Equal("in_progress", session.Status);
            Assert.Equal(0, session.RewardPoints);
        }

        [Fact]
        public void ClaimRewardResult_ShouldDeserializeCorrectly()
        {
            string json = "{\"success\": true, \"points_earned\": 25, \"new_total_points\": 125}";
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<ClaimRewardResult>(json);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(25, result.PointsEarned);
            Assert.Equal(125, result.NewTotalPoints);
        }
    }
}
