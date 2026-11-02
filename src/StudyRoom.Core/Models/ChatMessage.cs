namespace StudyRoom.Core.Models;

public class ChatMessage
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool IsSelf { get; set; }

    public ChatMessage()
    {
    }

    public ChatMessage(string senderId, string senderName, string content, bool isSelf = false)
    {
        MessageId = Guid.NewGuid().ToString("N");
        SenderId = senderId;
        SenderName = senderName;
        Content = content;
        Timestamp = DateTimeOffset.UtcNow;
        IsSelf = isSelf;
    }
}
