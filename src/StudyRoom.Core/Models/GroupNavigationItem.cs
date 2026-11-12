namespace StudyRoom.Core.Models;

public sealed class GroupNavigationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#6366F1";
    public string JoinCode { get; set; } = string.Empty;
    public bool IsPersonal { get; set; }
    public bool IsSelected { get; set; }
    public bool IsPinned { get; set; }
    public bool IsMuted { get; set; }
    public int UnreadCount { get; set; }
    public int MemberCount { get; set; }
    public bool IsHost { get; set; }
    public string HostName { get; set; } = string.Empty;

    public string ShortCode
    {
        get
        {
            if (IsPersonal) return "🏠";
            if (string.IsNullOrWhiteSpace(Name)) return "GR";
            var words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
            }
            return Name.Length <= 3 ? Name.ToUpperInvariant() : Name[..2].ToUpperInvariant();
        }
    }
}
