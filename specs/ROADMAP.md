# 🗺️ VIBE CODING PLAYBOOK & ROADMAP: StudyRoom Desktop App

> **Tài liệu lộ trình và cẩm nang thực thi Vibe Coding (Prompt-by-Task Execution Playbook)**  
> **Stack:** C# 12 · .NET 8 (LTS) · WPF (WPF-UI, MVVM CommunityToolkit) · AWS Serverless (Cognito, API Gateway, DynamoDB)  
> **Hướng dẫn sử dụng:** Khi lập trình với AI Agent (Cursor, Copilot, Antigravity, Claude Code), bạn chỉ cần chọn từng **Task Unit** tương ứng, sao chép **Vibe Prompt Template** và dán vào chat để Agent thực thi chuẩn xác.

---

## 📅 TỔNG QUAN TIẾN ĐỘ & CỘT MỐC (MILESTONES)

| Phase | Trọng tâm công việc | Cột mốc Git Tag | Trạng thái |
|---|---|---|---|
| **Phase 1** | Solution Setup, Clean Architecture, Git/CI, Base Project Structure | `v0.1.0-foundation` | ✅ **Đã hoàn thành** |
| **Phase 2** | DPAPI Storage, Business Engines (Pomodoro, Token Bucket, Fractional Index) | `v0.2.0-engines` | ✅ **Đã hoàn thành** |
| **Phase 3** | AWS CDK Infrastructure, HTTP REST & WebSocket Hub Lambdas | `v0.3.0-backend` | 🚀 **Đang thực hiện** |
| **Phase 4** | Client MVVM ViewModels, WPF-UI Screens, Real-time WebSocket Client | `v0.4.0-features` | 📋 Kế tiếp |
| **Phase 5** | Win32 Hotkey Hook, Topmost Floating Bubble Overlay, Messenger Integration | `v0.5.0-integration`| 📋 Kế tiếp |
| **Phase 6** | E2E Testing, Memory Profiling, Single-File Packaging & Inno Setup | `v1.0.0-release` | 📋 Chuẩn bị |

---

## 🧱 PHASE 1: FOUNDATION & SOLUTION ARCHITECTURE (ĐÃ HOÀN THÀNH)
*Đã thiết lập khung 4 project Clean Architecture, cấu hình nullable, Directory.Build.props và CI xanh.*
- [X] **TASK-1.1:** Khởi tạo Git repo, `.editorconfig`, `.gitignore`, `Directory.Build.props`.
- [X] **TASK-1.2:** Thiết lập `StudyRoom.sln` với 4 projects: `Core`, `Application`, `Infrastructure`, `UI` và test projects.
- [X] **TASK-1.3:** Tích hợp `WPF-UI` và `CommunityToolkit.Mvvm` vào `StudyRoom.UI`.
- [X] **TASK-1.4:** Unit Test nền tảng khởi tạo đạt trạng thái xanh (16 passed tests).

---

## ⚙️ PHASE 2: BẢO MẬT CỤC BỘ, LƯU TRỮ & BUSINESS ENGINES (ĐÃ HOÀN THÀNH)
- [X] **TASK-2.1: Windows DPAPI Secure Storage Service**
- [X] **TASK-2.2: Fractional Indexing Helper cho Kanban**
- [X] **TASK-2.3: Token Bucket Rate Limiter Chống Spam Chat**
- [X] **TASK-2.4: Authoritative Precision Pomodoro Engine**
- [X] **TASK-2.5: SQLite Local Cache Repository**

### 📌 TASK-2.1: Windows DPAPI Secure Storage Service (Hoàn thành)
- **Mục tiêu:** Cài đặt dịch vụ mã hóa lưu trữ token phiên đăng nhập vào `%LocalAppData%\StudyRoom\session.dat` bằng Windows DPAPI, tuyệt đối không lộ plaintext.
- **Target Files:**
  - `src/StudyRoom.Core/Models/AuthSession.cs`
  - `src/StudyRoom.Core/Interfaces/ISecureStorageService.cs`
  - `src/StudyRoom.Infrastructure/Security/DpapiSecureStorageService.cs`
  - `tests/StudyRoom.Infrastructure.Tests/DpapiSecureStorageServiceTests.cs`
- **Vibe Prompt Template:**
  ```markdown
  Bạn là Lead Desktop Architect. Hãy cài đặt `DpapiSecureStorageService` trong `StudyRoom.Infrastructure/Security/` triển khai interface `ISecureStorageService`.
  Yêu cầu:
  1. Sử dụng `System.Security.Cryptography.ProtectedData` với `DataProtectionScope.CurrentUser`.
  2. Dữ liệu lưu tại `%LocalAppData%\StudyRoom\session.dat`. Tự động tạo thư mục nếu chưa có.
  3. Xử lý async: `SaveSessionAsync`, `LoadSessionAsync`, `ClearSessionAsync` với `CancellationToken`.
  4. Viết Unit Test trong `DpapiSecureStorageServiceTests.cs` kiểm chứng: lưu và đọc lại khớp dữ liệu; file trên đĩa được mã hóa nhị phân không chứa chuỗi plaintext token.
  Tham khảo spec: `specs/architecture.md` mục 3.1 và `specs/database_schema.md` mục 4.1.
  ```
- **Lệnh kiểm chứng:** `dotnet test StudyRoom.sln --filter "FullyQualifiedName~DpapiSecureStorageServiceTests"`
- **DoD:** Test lưu/đọc/xóa pass 100%; không rò rỉ plaintext.

---

### 📌 TASK-2.2: Fractional Indexing Helper cho Kanban
- **Mục tiêu:** Cài đặt thuật toán tính `Rank` số thực (`double`) khi kéo thả thẻ Kanban và cơ chế tự động cân bằng (Rebalance).
- **Target Files:**
  - `src/StudyRoom.Core/Interfaces/IFractionalIndexingHelper.cs`
  - `src/StudyRoom.Application/Helpers/FractionalIndexingHelper.cs`
  - `tests/StudyRoom.Application.Tests/FractionalIndexingTests.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy cài đặt `FractionalIndexingHelper` trong `StudyRoom.Application/Helpers/` triển khai `IFractionalIndexingHelper`.
  Yêu cầu nghiệp vụ:
  - `CalculateNewRank(double? prevRank, double? nextRank)`:
    - Rỗng: trả về 1.0.
    - Chèn đầu (chỉ có nextRank): trả về nextRank / 2.0.
    - Chèn cuối (chỉ có prevRank): trả về prevRank + 1.0.
    - Chèn giữa: trả về (prevRank + nextRank) / 2.0.
  - `NeedsRebalance(IEnumerable<double> ranks)`: trả về true nếu khoảng cách giữa 2 phần tử kề nhau < 1e-6.
  - `Rebalance(int count)`: trả về danh sách rank phân bổ đều `1.0, 2.0, 3.0, ...`.
  - Viết bộ Unit Test đầy đủ các trường hợp trong `FractionalIndexingTests.cs`.
  Tham khảo spec: `specs/database_schema.md` mục 5.2.
  ```
- **Lệnh kiểm chứng:** `dotnet test StudyRoom.sln --filter "FullyQualifiedName~FractionalIndexingTests"`
- **DoD:** Tất cả test case chèn đầu/giữa/cuối/rỗng và rebalance đều pass.

---

### 📌 TASK-2.3: Token Bucket Rate Limiter Chống Spam Chat
- **Mục tiêu:** Cài đặt thuật toán Token Bucket ở client để kiểm soát tần suất gửi tin nhắn chat, ngăn làm nghẽn kết nối WebSocket.
- **Target Files:**
  - `src/StudyRoom.Core/Interfaces/IRateLimiter.cs`
  - `src/StudyRoom.Application/Engines/TokenBucketRateLimiter.cs`
  - `tests/StudyRoom.Application.Tests/TokenBucketRateLimiterTests.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy cài đặt `TokenBucketRateLimiter` trong `StudyRoom.Application/Engines/` triển khai `IRateLimiter`.
  Yêu cầu:
  1. Cấu hình mặc định: dung lượng tối đa = 5 tokens; tốc độ nạp = 1 token/giây. Hỗ trợ cấu hình qua constructor.
  2. Phương thức `bool TryAcquire()`: trừ 1 token nếu còn; trả về false nếu hết. Thread-safe bằng `lock`.
  3. `TimeSpan GetRetryAfter()`: tính thời gian cần chờ đến khi có ít nhất 1 token.
  4. Viết Unit Test `TokenBucketRateLimiterTests.cs`: kiểm tra burst 5 tin nhắn liên tiếp thành công, tin thứ 6 bị chặn, sau 1 giây được nạp lại và gửi tiếp được.
  Tham khảo spec: `specs/architecture.md` mục 3.3.
  ```
- **Lệnh kiểm chứng:** `dotnet test StudyRoom.sln --filter "FullyQualifiedName~TokenBucketRateLimiterTests"`
- **DoD:** Unit test kiểm chứng burst, chặn spam và refill token pass 100%.

---

### 📌 TASK-2.4: Authoritative Precision Pomodoro Engine
- **Mục tiêu:** Nâng cấp động cơ đếm lùi Pomodoro sử dụng `PeriodicTimer` (chu kỳ 200ms) tính theo mốc `TargetEndTimeUtc`, hỗ trợ bù trôi thời gian (clock drift) và chạy chuẩn khi mất mạng.
- **Target Files:**
  - `src/StudyRoom.Core/Interfaces/IPomodoroEngine.cs`
  - `src/StudyRoom.Application/Engines/PomodoroEngine.cs`
  - `tests/StudyRoom.Application.Tests/PomodoroEngineTests.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy xây dựng `PomodoroEngine` trong `StudyRoom.Application/Engines/` triển khai `IPomodoroEngine` (và kế thừa/thay thế `LocalPomodoroTimerService` một cách tương thích).
  Yêu cầu:
  1. Sử dụng `System.Threading.PeriodicTimer` với chu kỳ 200ms chạy trên background task để đếm thời gian mượt mà.
  2. Thời gian còn lại tính toán chính xác: `(TargetEndTimeUtc - DateTimeOffset.UtcNow).TotalSeconds`.
  3. Hỗ trợ phương thức `SyncFromServer(DateTimeOffset? targetEndTimeUtc, PomodoroPhase phase, TimerState state)`: hiệu chỉnh đồng hồ cục bộ nếu phát hiện lệch > 500ms.
  4. Quản lý chuyển pha: Focus (25m) -> ShortBreak (5m) -> Focus -> ... (sau 4 chu kỳ Focus thì sang LongBreak 15m).
  5. Phương thức `CanControl(userId, group)`: kiểm tra quyền Host/BatonHolder hoặc Equal mode.
  6. Viết đầy đủ Unit Test trong `PomodoroEngineTests.cs` (kiểm tra Start, Pause, Resume, Reset, Skip, SyncFromServer, Offline Resilience).
  Tham khảo spec: `specs/architecture.md` mục 3.3 & 5.
  ```
- **Lệnh kiểm chứng:** `dotnet test StudyRoom.sln --filter "FullyQualifiedName~PomodoroEngineTests"`
- **DoD:** Kiểm tra đếm lùi chính xác, bù lệch mốc giờ, chuyển pha tự động pass 100%.

---

### 📌 TASK-2.5: SQLite Local Cache Repository
- **Mục tiêu:** Tạo database cục bộ `%LocalAppData%\StudyRoom\studyroom_local.db` để lưu cache offline cho bảng Kanban và Flashcard.
- **Target Files:**
  - `src/StudyRoom.Infrastructure/Storage/SqliteLocalCacheRepository.cs`
  - `tests/StudyRoom.Infrastructure.Tests/SqliteLocalCacheRepositoryTests.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy cài đặt `SqliteLocalCacheRepository` trong `StudyRoom.Infrastructure/Storage/` sử dụng `Microsoft.Data.Sqlite`.
  Yêu cầu:
  1. Tự động khởi tạo database và thực thi schema DDL từ `specs/database_schema.md` mục 4.2 (`UserSettings`, `CachedKanbanCards`, `CachedFlashcards`).
  2. Cung cấp các phương thức async: `CacheCardsAsync(groupId, cards)`, `GetCachedCardsAsync(groupId)`, `SaveSettingAsync(key, value)`, `GetSettingAsync(key)`.
  3. Viết Unit Test xác thực việc lưu và đọc lại danh sách thẻ offline.
  ```
- **Lệnh kiểm chứng:** `dotnet test StudyRoom.sln --filter "FullyQualifiedName~SqliteLocalCacheRepositoryTests"`

---

## ☁️ PHASE 3: AWS CLOUD BACKEND (SERVERLESS & WEBSOCKET HUB)

### 📌 TASK-3.1: AWS CDK Infrastructure Deployment
- **Target Files:**
  - `infra/src/StudyRoomInfra/StudyRoomStack.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy xây dựng hạ tầng AWS CDK (.NET C#) trong thư mục `infra/`:
  1. Tạo bảng DynamoDB `StudyRoom_Connections` (PK: ConnectionId, TTL: ExpiresAt, GSI1: RoomId/ConnectionId).
  2. Tạo bảng DynamoDB `StudyRoom_Data` (PK: PK, SK: SK).
  3. Tạo Cognito User Pool (đăng nhập bằng email) kèm App Client không có client secret.
  4. Tạo API Gateway WebSocket API và HTTP API kèm quyền gọi Lambda theo Least Privilege.
  Tham khảo spec: `specs/database_schema.md` mục 1.
  ```

### 📌 TASK-3.2: HTTP REST Lambdas (Group & Member APIs)
- **Target Files:**
  - `backend/src/StudyRoom.Backend/Handlers/GroupHandlers.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy viết các AWS Lambda handlers cho HTTP REST API trong `backend/`:
  - `POST /groups`: Tạo nhóm mới, ghi `METADATA` và `MEMBER#{ownerId}` vào DynamoDB.
  - `GET /groups/{id}`: Thực hiện 1 truy vấn `Query(PK = "GROUP#{id}")` trả về Group, Members, Cards, Deck.
  - `POST /groups/{id}/members`: Thêm thành viên vào nhóm.
  Tham khảo spec: `specs/database_schema.md` mục 1.2 & 3.
  ```

### 📌 TASK-3.3: WebSocket Hub Lambdas (Real-time Broadcast & Relay)
- **Target Files:**
  - `backend/src/StudyRoom.Backend/Handlers/WebSocketHandlers.cs`
- **Vibe Prompt Template:**
  ```markdown
  Hãy viết các Lambda xử lý WebSocket:
  - `$connect`: Lưu `ConnectionId`, `RoomId`, `UserId` vào `StudyRoom_Connections` với TTL 24h.
  - `$disconnect`: Xóa kết nối khỏi `StudyRoom_Connections`.
  - Các route handler: `JOIN_ROOM`, `START_POMODORO`, `PAUSE_POMODORO`, `PASS_BATON`, `MOVE_KANBAN_CARD`, `SEND_CHAT` (relay trong RAM, không lưu DB), `TRIGGER_FLASHCARD`.
  - Sử dụng `AmazonApiGatewayManagementApiClient` để broadcast bản tin tới danh sách socket trong phòng; bắt `GoneException` để tự động dọn socket chết.
  Tham khảo spec: `specs/database_schema.md` mục 2 (WebSocket Wire Protocol).
  ```

---

## 🧩 PHASE 4: CLIENT MODULE IMPLEMENTATION & VIEWS

### 📌 TASK-4.1: Cognito Auth & LoginViewModel
- **Target Files:**
  - `src/StudyRoom.Infrastructure/Auth/CognitoAuthService.cs`
  - `src/StudyRoom.UI/ViewModels/LoginViewModel.cs`
  - `src/StudyRoom.UI/Views/LoginView.xaml(.cs)`
- **Vibe Prompt:** "Cài đặt `CognitoAuthService` và `LoginViewModel` hỗ trợ đăng nhập, đăng ký, lưu DPAPI và chuyển view."

### 📌 TASK-4.2: DashboardViewModel (Room Selection & Creation)
- **Target Files:**
  - `src/StudyRoom.UI/ViewModels/DashboardViewModel.cs`
  - `src/StudyRoom.UI/Views/DashboardView.xaml(.cs)`
- **Vibe Prompt:** "Cài đặt `DashboardViewModel` với danh sách phòng gần đây, form tạo phòng (chọn Host-Only/Equal) và form tham gia phòng."

### 📌 TASK-4.3: Real-time WebSocket Client
- **Target Files:**
  - `src/StudyRoom.Infrastructure/Network/WebSocketClient.cs`
  - `tests/StudyRoom.Infrastructure.Tests/WebSocketClientTests.cs`
- **Vibe Prompt:** "Cài đặt `WebSocketClient` dùng `ClientWebSocket` hỗ trợ tự kết nối lại (Exponential Backoff + Jitter), Ping/Pong heartbeat và phân phối gói tin qua `MessageReceived`."

### 📌 TASK-4.4: PomodoroViewModel & Radial Clock Control
- **Target Files:**
  - `src/StudyRoom.UI/Controls/PomodoroClockControl.xaml(.cs)`
  - `src/StudyRoom.UI/ViewModels/PomodoroViewModel.cs`
- **Vibe Prompt:** "Nâng cấp giao diện Pomodoro với thanh tiến trình tròn (Radial Progress), đổi màu Xanh dịu (Focus) <-> Vàng ấm (Break), phát âm thanh chuông khi chuyển pha."

### 📌 TASK-4.5: KanbanViewModel & Drag-and-Drop Column Control
- **Target Files:**
  - `src/StudyRoom.UI/Controls/KanbanColumnControl.xaml(.cs)`
  - `src/StudyRoom.UI/Behaviors/KanbanDragDropBehavior.cs`
  - `src/StudyRoom.UI/ViewModels/KanbanViewModel.cs`
- **Vibe Prompt:** "Cài đặt kéo thả thẻ Kanban giữa 3 cột Todo/InProgress/Done, tính NewRank qua `FractionalIndexingHelper` và gửi `MOVE_KANBAN_CARD`."

### 📌 TASK-4.6: FlashcardViewModel & 3D Flip Card Dialog
- **Target Files:**
  - `src/StudyRoom.UI/Views/FlashcardDialog.xaml(.cs)`
  - `src/StudyRoom.UI/ViewModels/FlashcardViewModel.cs`
- **Vibe Prompt:** "Cài đặt pop-up Flashcard với hiệu ứng lật thẻ 3D bằng WPF Storyboard khi nhận sự kiện `FLASHCARD_PROMPT`."

---

## 🔗 PHASE 5: SYSTEM INTEGRATION & OVERLAYS

### 📌 TASK-5.1: Win32 Global HotKey Service
- **Target Files:**
  - `src/StudyRoom.Infrastructure/OS/Win32HotKeyService.cs`
  - `src/StudyRoom.UI/MainWindow.xaml.cs` (Hook `HwndSource`)
- **Vibe Prompt:** "Cài đặt P/Invoke `RegisterHotKey` / `UnregisterHotKey` bắt tổ hợp phím `Ctrl+Shift+C` và `Ctrl+Shift+P` an toàn."

### 📌 TASK-5.2: Topmost Floating Bubble Chat Window
- **Target Files:**
  - `src/StudyRoom.UI/Views/FloatingBubbleView.xaml(.cs)`
  - `src/StudyRoom.UI/ViewModels/FloatingBubbleViewModel.cs`
- **Vibe Prompt:** "Cài đặt cửa sổ bong bóng chat nổi (`Topmost=true`, `AllowsTransparency=true`), kéo thả được và tích hợp `TokenBucketRateLimiter`."

### 📌 TASK-5.3: Cross-Module Messenger Orchestration
- **Target Files:**
  - Các ViewModels đăng ký nhận `WeakReferenceMessenger.Default` và hủy đăng ký khi unload.
- **Vibe Prompt:** "Kết nối Pomodoro, Chat, Kanban thông qua các message trong `StudyRoom.Application/Messages/` mà không để ViewModel tham chiếu chéo nhau."

---

## 🚀 PHASE 6: HARDENING, PACKAGING & RELEASE

- [ ] **TASK-6.1:** Kiểm tra rò rỉ bộ nhớ qua dotMemory / Diagnostics; đảm bảo RAM chạy nền < 80MB.
- [ ] **TASK-6.2:** Cấu hình Publish Single-File: `dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`.
- [ ] **TASK-6.3:** Viết kịch bản đóng gói Inno Setup `setup.iss` tạo `StudyRoomSetup.exe`.
- [ ] **TASK-6.4:** Đóng gói bản phát hành `v1.0.0` trên GitHub Releases kèm checksum SHA-256.
