# StudyRoom 💻📚 

**StudyRoom** là ứng dụng Desktop hỗ trợ học tập nhóm từ xa, được thiết kế dành riêng cho học sinh và sinh viên muốn tối ưu hóa hiệu suất làm việc nhóm, quản lý thời gian khoa học và duy trì sự tập trung tối đa. 

## ✨ Tính Năng Nổi Bật (MVP & Mở Rộng) 
* 🔐 **Xác thực an toàn:** Tích hợp quản lý tài khoản và định danh người dùng mạnh mẽ. 
* 👥 **Quản lý Nhóm học tập:** Tạo nhóm linh hoạt với tùy chọn phân quyền điều khiển (Trưởng nhóm hoặc chế độ bình đẳng). 
* 📋 **Bảng Kanban trực quan:** Kéo thả thẻ công việc, đồng bộ hóa dữ liệu thời gian thực giữa các thành viên. 
* ⏱️ **Phòng Pomodoro Đồng Bộ Nhóm:** Chu kỳ học/nghỉ tùy chỉnh, tính năng nhường quyền điều khiển (*Pass the baton*), chuyển đổi trạng thái tự động kèm giao diện thay đổi linh hoạt. 
* 💬 **Giao tiếp thông minh:** Tích hợp bong bóng chat nổi cho pha tập trung (hạn chế xao lãng) và tự động bung khung chat lớn trong giờ nghỉ. 
* 🧠 **Hệ thống Flashcard Nhóm:** Tính năng kiểm tra chéo kiến thức ngẫu nhiên giúp ôn tập hiệu quả. 

## 🛠️ Công Nghệ Sử Dụng 
* **Frontend (Desktop App):** C# (.NET 8) với WPF, hỗ trợ chạy trên hệ điều hành Windows. 
* **Backend & Cloud (AWS):** 
* **Amazon Cognito:** Xác thực và bảo mật tài khoản. 
* **Amazon DynamoDB:** Lưu trữ dữ liệu nhóm, trạng thái và cấu hình. 
* **AWS API Gateway WebSocket & Lambda:** Đồng bộ hóa dữ liệu thời gian thực (Real-time).