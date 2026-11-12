using StudyRoom.Core.Models;

namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi có tin nhắn chat mới nhận từ WebSocket hoặc người dùng gửi.
/// </summary>
public sealed record ChatMessageReceivedMessage(ChatMessage Message);
