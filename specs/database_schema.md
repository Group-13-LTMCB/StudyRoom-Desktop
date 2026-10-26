# DATABASE & STORAGE SCHEMA: StudyRoom Desktop App

> **Tài liệu đặc tả lưu trữ đám mây, bộ nhớ đệm cục bộ và giao thức truyền tin thời gian thực**  
> **Phiên bản:** 2.0  
> **Mục tiêu:** Định nghĩa chính xác cấu trúc dữ liệu DynamoDB, SQLite cục bộ, DPAPI và toàn bộ gói tin WebSocket/REST API để AI Agent sinh mã chuẩn xác tuyệt đối.

---

## 1. CLOUD STORAGE LAYER: AMAZON DYNAMODB

Hệ thống backend sử dụng 2 bảng DynamoDB để phân tách giữa quản lý kết nối socket ngắn hạn và dữ liệu nghiệp vụ lâu dài.

### 1.1. Bảng 1: `StudyRoom_Connections` (Quản lý Socket Trực tuyến)
- **Mục đích:** Lưu trữ danh sách các kết nối WebSocket đang hoạt động để broadcast dữ liệu trong từng phòng.
- **Tính năng bật:** **Time-to-Live (TTL)** trên thuộc tính `ExpiresAt`.

| Tên thuộc tính | Kiểu DynamoDB | Khóa / Chỉ mục | Ràng buộc & Ghi chú |
|---|---|---|---|
| `ConnectionId` | **String (S)** | **Partition Key (PK)** | ID định danh socket do API Gateway sinh ra |
| `RoomId` | String (S) | **GSI1_PK** | Mã phòng học (`GroupId`) |
| `UserId` | String (S) | Attribute | User ID từ Cognito |
| `ConnectedAt` | String (S) | Attribute | Mốc thời gian ISO8601 UTC |
| `ExpiresAt` | **Number (N)** | **TTL Key** | Epoch timestamp (giây). Tự động xóa sau 24 giờ nếu ngắt kết nối đột ngột |

- **Global Secondary Index (GSI1):**
  - **Partition Key (GSI1_PK):** `RoomId` (String)
  - **Sort Key (GSI1_SK):** `ConnectionId` (String)
  - **Truy vấn broadcast:** `Query(GSI1_PK = :roomId)` để lấy toàn bộ `ConnectionId` cần gửi tin trong 1 phòng.

---

### 1.2. Bảng 2: `StudyRoom_Data` (Single-Table Design cho Nghiệp vụ)
- **Mục đích:** Chứa toàn bộ thông tin nhóm, thành viên, thẻ Kanban và bộ thẻ Flashcard chỉ trong 1 bảng duy nhất.
- **Cấu trúc khóa chính kết hợp (Composite Primary Key):**
  - **Partition Key (PK):** `GROUP#{GroupId}`
  - **Sort Key (SK):** Định danh theo loại thực thể (`METADATA`, `MEMBER#{UserId}`, `CARD#{CardId}`, `DECK#{CardId}`).

#### Chi tiết các dòng dữ liệu trong `StudyRoom_Data`:

| Loại thực thể | Partition Key (PK) | Sort Key (SK) | Danh sách thuộc tính & Kiểu dữ liệu |
|---|---|---|---|
| **Nhóm học (Group Metadata)** | `GROUP#{GroupId}` | `METADATA` | - `GroupId` (S): ID phòng<br>- `GroupName` (S): Tên nhóm học<br>- `OwnerId` (S): UserId người tạo<br>- `ControlMode` (S): `"HOST_ONLY"` \| `"EQUAL"`<br>- `CurrentHostId` (S): UserId người giữ quyền Pomodoro<br>- `FocusDurationMinutes` (N): Thời lượng tập trung (VD: 25)<br>- `BreakDurationMinutes` (N): Thời lượng nghỉ ngơi (VD: 5)<br>- `CurrentPhase` (S): `"Focus"` \| `"ShortBreak"` \| `"LongBreak"`<br>- `TimerState` (S): `"Stopped"` \| `"Running"` \| `"Paused"`<br>- `TargetEndTimeUtc` (S, Nullable): Mốc kết thúc ISO8601 UTC<br>- `CreatedAt` (S): ISO8601 UTC |
| **Thành viên (Room Member)** | `GROUP#{GroupId}` | `MEMBER#{UserId}` | - `UserId` (S): Cognito User ID<br>- `Role` (S): `"Host"` \| `"Member"`<br>- `DisplayName` (S): Tên hiển thị của thành viên<br>- `AvatarUrl` (S, Nullable): Đường dẫn ảnh đại diện<br>- `JoinedAt` (S): ISO8601 UTC |
| **Thẻ Kanban (Kanban Card)** | `GROUP#{GroupId}` | `CARD#{CardId}` | - `CardId` (S): ID định danh thẻ (Guid N string)<br>- `ColumnId` (S): `"Todo"` \| `"InProgress"` \| `"Done"`<br>- `Title` (S): Tiêu đề công việc<br>- `Description` (S, Nullable): Mô tả chi tiết<br>- `Rank` (N): Số thực (double) dùng cho Fractional Indexing<br>- `AssigneeId` (S, Nullable): UserId người nhận nhiệm vụ<br>- `CreatedAt` (S): ISO8601 UTC<br>- `UpdatedAt` (S): ISO8601 UTC |
| **Thẻ Flashcard (Deck Card)**| `GROUP#{GroupId}` | `DECK#{CardId}` | - `CardId` (S): ID định danh thẻ câu hỏi<br>- `Question` (S): Nội dung câu hỏi<br>- `Answer` (S): Nội dung đáp án<br>- `CreatedBy` (S): UserId người tạo<br>- `CreatedAt` (S): ISO8601 UTC<br>- `LastReviewedAt` (S, Nullable): ISO8601 UTC |

> **Quy tắc Single Query:** Khi mở phòng học, client chỉ cần 1 truy vấn duy nhất:  
> `Query(PK = "GROUP#{groupId}")` là lấy được toàn bộ Metadata, danh sách Members, thẻ Kanban và Flashcard mà không cần JOIN nhiều bảng.

---

## 2. REAL-TIME WEAPON: GIAO THỨC WEBSOCKET WIRE PROTOCOL

Mọi gói tin giao tiếp qua WebSocket đều tuân theo chuẩn đóng gói phong bì (Envelope Standard):
```json
{
  "action": "<ACTION_NAME>",
  "roomId": "<GROUP_ID>",
  "data": { ... }
}
```

Dưới đây là đặc tả chi tiết từng Action để AI Agent lập trình đúng chuẩn JSON serialization (thuộc tính dùng camelCase):

### 2.1. Tham gia phòng (`JOIN_ROOM`)
- **Client gửi lên:**
```json
{
  "action": "JOIN_ROOM",
  "roomId": "room_a1b2c3d4",
  "data": {
    "userId": "user_cognito_001",
    "displayName": "Nguyen Van A"
  }
}
```
- **Server broadcast tới các máy khác:**
```json
{
  "action": "MEMBER_JOINED",
  "roomId": "room_a1b2c3d4",
  "data": {
    "userId": "user_cognito_001",
    "displayName": "Nguyen Van A",
    "joinedAt": "2026-10-26T08:00:00Z"
  }
}
```

### 2.2. Điều khiển Pomodoro (`START_POMODORO`, `PAUSE_POMODORO`, `POMODORO_SYNC`)
- **Client gửi lệnh bắt đầu:**
```json
{
  "action": "START_POMODORO",
  "roomId": "room_a1b2c3d4",
  "data": {
    "requestedBy": "user_cognito_001"
  }
}
```
- **Server kiểm tra quyền và broadcast đồng bộ (`POMODORO_SYNC`):**
```json
{
  "action": "POMODORO_SYNC",
  "roomId": "room_a1b2c3d4",
  "data": {
    "currentPhase": "Focus",
    "timerState": "Running",
    "targetEndTimeUtc": "2026-10-26T08:25:00Z",
    "totalDurationSeconds": 1500,
    "completedFocusCycles": 0,
    "currentHostId": "user_cognito_001"
  }
}
```
- **Client gửi lệnh tạm dừng:**
```json
{
  "action": "PAUSE_POMODORO",
  "roomId": "room_a1b2c3d4",
  "data": {
    "requestedBy": "user_cognito_001",
    "remainingSeconds": 850
  }
}
```

### 2.3. Nhường quyền điều khiển Pomodoro (`PASS_BATON`)
- **Client gửi lên:**
```json
{
  "action": "PASS_BATON",
  "roomId": "room_a1b2c3d4",
  "data": {
    "currentHostId": "user_cognito_001",
    "newHostUserId": "user_cognito_002"
  }
}
```
- **Server broadcast:**
```json
{
  "action": "BATON_PASSED",
  "roomId": "room_a1b2c3d4",
  "data": {
    "previousHostId": "user_cognito_001",
    "newHostUserId": "user_cognito_002"
  }
}
```

### 2.4. Kéo thả thẻ Kanban (`MOVE_KANBAN_CARD`)
- **Client gửi lên sau khi kéo thả:**
```json
{
  "action": "MOVE_KANBAN_CARD",
  "roomId": "room_a1b2c3d4",
  "data": {
    "cardId": "card_987654",
    "newColumn": "InProgress",
    "newRank": 1.5,
    "movedBy": "user_cognito_001"
  }
}
```
- **Server ghi DynamoDB và broadcast `CARD_MOVED`:**
```json
{
  "action": "CARD_MOVED",
  "roomId": "room_a1b2c3d4",
  "data": {
    "cardId": "card_987654",
    "newColumn": "InProgress",
    "newRank": 1.5,
    "updatedAt": "2026-10-26T08:05:00Z"
  }
}
```

### 2.5. Chat tức thời không lưu DB (`SEND_CHAT`)
- **Client gửi tin nhắn:**
```json
{
  "action": "SEND_CHAT",
  "roomId": "room_a1b2c3d4",
  "data": {
    "senderId": "user_cognito_001",
    "senderName": "Nguyen Van A",
    "content": "Chào mọi người, cùng tập trung nhé!",
    "timestamp": "2026-10-26T08:01:00Z"
  }
}
```
- **Server relay trong RAM phát lại cho toàn phòng:**
```json
{
  "action": "CHAT_MESSAGE",
  "roomId": "room_a1b2c3d4",
  "data": {
    "messageId": "msg_uuid_1234",
    "senderId": "user_cognito_001",
    "senderName": "Nguyen Van A",
    "content": "Chào mọi người, cùng tập trung nhé!",
    "timestamp": "2026-10-26T08:01:00Z"
  }
}
```

### 2.6. Thử thách Flashcard trong giờ giải lao (`TRIGGER_FLASHCARD`)
- **Client bấm thử thách:**
```json
{
  "action": "TRIGGER_FLASHCARD",
  "roomId": "room_a1b2c3d4",
  "data": {
    "triggeredBy": "user_cognito_001"
  }
}
```
- **Server bốc ngẫu nhiên từ DynamoDB và broadcast:**
```json
{
  "action": "FLASHCARD_PROMPT",
  "roomId": "room_a1b2c3d4",
  "data": {
    "cardId": "deck_card_007",
    "question": "Deadlock trong lập trình đa luồng xảy ra khi nào?",
    "answer": "Khi hai hay nhiều luồng giữ tài nguyên và cùng chờ tài nguyên của luồng kia mà không nhả ra.",
    "timeoutSeconds": 30
  }
}
```

---

## 3. REST API ENDPOINTS SPECIFICATION

Dùng cho việc khởi tạo phòng, tải dữ liệu ban đầu hoặc đối soát sau khi mất mạng:

| Method | Endpoint | Xác thực | Mô tả chức năng | Request Body / Parameters | Phản hồi (200 OK) |
|---|---|---|---|---|---|
| **POST** | `/groups` | Bearer JWT | Tạo phòng học mới | `{ "groupName": string, "controlMode": "HostOnly" \| "Equal", "focusDurationMinutes": 25, "breakDurationMinutes": 5 }` | `{ "groupId": string, "groupName": string, "ownerId": string, "joinCode": string }` |
| **GET** | `/groups/{id}` | Bearer JWT | Tải thông tin phòng và toàn bộ thẻ | Không có | `{ "group": StudyGroup, "members": Member[], "cards": KanbanCard[], "deck": Flashcard[] }` |
| **POST** | `/groups/{id}/members` | Bearer JWT | Gia nhập phòng bằng ID | `{ "displayName": string }` | `{ "success": true, "role": "Member" }` |

---

## 4. LOCAL CLIENT STORAGE: SECURE DPAPI & SQLITE CACHE

### 4.1. File mã hóa phiên làm việc (`%LocalAppData%\StudyRoom\session.dat`)
DTO `AuthSession` được chuyển thành JSON bytes và mã hóa bằng:
`ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)`.

```csharp
namespace StudyRoom.Core.Models;

public sealed record AuthSession
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string IdToken { get; init; }
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
}
```

### 4.2. Cơ sở dữ liệu SQLite cục bộ (`%LocalAppData%\StudyRoom\studyroom_local.db`)
Hỗ trợ lưu cấu hình và cache phòng học gần nhất để mở app ở chế độ offline:

```sql
-- 1. Bảng lưu cấu hình người dùng và phím tắt
CREATE TABLE IF NOT EXISTS UserSettings (
    Key TEXT PRIMARY KEY NOT NULL,
    Value TEXT NOT NULL,
    LastUpdatedUtc TEXT NOT NULL
);

-- 2. Bảng cache thẻ Kanban của phòng gần nhất
CREATE TABLE IF NOT EXISTS CachedKanbanCards (
    CardId TEXT PRIMARY KEY NOT NULL,
    GroupId TEXT NOT NULL,
    ColumnId TEXT NOT NULL CHECK(ColumnId IN ('Todo', 'InProgress', 'Done')),
    Title TEXT NOT NULL,
    Description TEXT,
    Rank REAL NOT NULL,
    AssigneeId TEXT,
    UpdatedAtUtc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_CachedKanbanCards_GroupId_ColumnId 
ON CachedKanbanCards (GroupId, ColumnId, Rank ASC);

-- 3. Bảng cache danh sách Flashcard của phòng
CREATE TABLE IF NOT EXISTS CachedFlashcards (
    CardId TEXT PRIMARY KEY NOT NULL,
    GroupId TEXT NOT NULL,
    Question TEXT NOT NULL,
    Answer TEXT NOT NULL,
    LastReviewedUtc TEXT
);
```

---

## 5. C# DOMAIN POCOS & FRACTIONAL INDEXING ALGORITHM

### 5.1. C# Domain Entities (`StudyRoom.Core/Entities/`)

```csharp
namespace StudyRoom.Core.Entities;

public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StudyGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string HostUserId { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public ControlMode ControlMode { get; set; } = ControlMode.HostOnly;
    public string CurrentHostId { get; set; } = string.Empty;
    public int FocusDurationMinutes { get; set; } = 25;
    public int BreakDurationMinutes { get; set; } = 5;
    public PomodoroPhase CurrentPhase { get; set; } = PomodoroPhase.Focus;
    public TimerState TimerState { get; set; } = TimerState.Stopped;
    public DateTimeOffset? TargetEndTimeUtc { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Member
{
    public string UserId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public MemberRole Role { get; set; } = MemberRole.Member;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class KanbanCard
{
    public string CardId { get; set; } = Guid.NewGuid().ToString("N");
    public string GroupId { get; set; } = string.Empty;
    public KanbanColumn ColumnId { get; set; } = KanbanColumn.Todo;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Rank { get; set; } = 1.0;
    public string? AssigneeId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Flashcard
{
    public string CardId { get; set; } = Guid.NewGuid().ToString("N");
    public string GroupId { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReviewedAt { get; set; }
}
```

### 5.2. Thuật toán Fractional Indexing Chuẩn Mực

```csharp
namespace StudyRoom.Application.Helpers;

public class FractionalIndexingHelper : IFractionalIndexingHelper
{
    private const double Epsilon = 1e-6;

    public double CalculateNewRank(double? prevRank, double? nextRank)
    {
        if (prevRank.HasValue && nextRank.HasValue)
        {
            return (prevRank.Value + nextRank.Value) / 2.0;
        }

        if (prevRank.HasValue && !nextRank.HasValue)
        {
            return prevRank.Value + 1.0;
        }

        if (!prevRank.HasValue && nextRank.HasValue)
        {
            return nextRank.Value / 2.0;
        }

        // Danh sách rỗng: gán giá trị mặc định đầu tiên
        return 1.0;
    }

    public bool NeedsRebalance(IEnumerable<double> ranks)
    {
        var sorted = ranks.OrderBy(r => r).ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (Math.Abs(sorted[i + 1] - sorted[i]) < Epsilon)
            {
                return true;
            }
        }
        return false;
    }

    public List<double> Rebalance(int count)
    {
        var result = new List<double>(count);
        for (int i = 1; i <= count; i++)
        {
            result.Add(i * 1.0);
        }
        return result;
    }
}
```
