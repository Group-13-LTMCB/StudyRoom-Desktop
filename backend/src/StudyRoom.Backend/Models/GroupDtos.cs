using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StudyRoom.Backend.Models;

public sealed record CreateGroupRequest
{
    [JsonPropertyName("groupName")]
    public string GroupName { get; init; } = string.Empty;

    [JsonPropertyName("controlMode")]
    public string? ControlMode { get; init; }

    [JsonPropertyName("focusDurationMinutes")]
    public int FocusDurationMinutes { get; init; } = 25;

    [JsonPropertyName("breakDurationMinutes")]
    public int BreakDurationMinutes { get; init; } = 5;

    [JsonPropertyName("ownerId")]
    public string? OwnerId { get; init; }

    [JsonPropertyName("ownerDisplayName")]
    public string? OwnerDisplayName { get; init; }
}

public sealed record CreateGroupResponse
{
    [JsonPropertyName("groupId")]
    public required string GroupId { get; init; }

    [JsonPropertyName("groupName")]
    public required string GroupName { get; init; }

    [JsonPropertyName("ownerId")]
    public required string OwnerId { get; init; }

    [JsonPropertyName("joinCode")]
    public required string JoinCode { get; init; }
}

public sealed record AddMemberRequest
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("userId")]
    public string? UserId { get; init; }
}

public sealed record AddMemberResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("role")]
    public string Role { get; init; } = "Member";
}

public sealed record GroupMetadataDto
{
    [JsonPropertyName("groupId")]
    public string GroupId { get; init; } = string.Empty;

    [JsonPropertyName("groupName")]
    public string GroupName { get; init; } = string.Empty;

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; init; } = string.Empty;

    [JsonPropertyName("controlMode")]
    public string ControlMode { get; init; } = "HOST_ONLY";

    [JsonPropertyName("currentHostId")]
    public string CurrentHostId { get; init; } = string.Empty;

    [JsonPropertyName("focusDurationMinutes")]
    public int FocusDurationMinutes { get; init; } = 25;

    [JsonPropertyName("breakDurationMinutes")]
    public int BreakDurationMinutes { get; init; } = 5;

    [JsonPropertyName("currentPhase")]
    public string CurrentPhase { get; init; } = "Focus";

    [JsonPropertyName("timerState")]
    public string TimerState { get; init; } = "Stopped";

    [JsonPropertyName("targetEndTimeUtc")]
    public string? TargetEndTimeUtc { get; init; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("joinCode")]
    public string? JoinCode { get; init; }
}

public sealed record MemberDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = "Member";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("joinedAt")]
    public string JoinedAt { get; init; } = string.Empty;
}

public sealed record KanbanCardDto
{
    [JsonPropertyName("cardId")]
    public string CardId { get; init; } = string.Empty;

    [JsonPropertyName("columnId")]
    public string ColumnId { get; init; } = "Todo";

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("rank")]
    public double Rank { get; init; } = 1.0;

    [JsonPropertyName("assigneeId")]
    public string? AssigneeId { get; init; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed record FlashcardDto
{
    [JsonPropertyName("cardId")]
    public string CardId { get; init; } = string.Empty;

    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("answer")]
    public string Answer { get; init; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("lastReviewedAt")]
    public string? LastReviewedAt { get; init; }
}

public sealed record GroupDetailResponse
{
    [JsonPropertyName("group")]
    public required GroupMetadataDto Group { get; init; }

    [JsonPropertyName("members")]
    public required List<MemberDto> Members { get; init; }

    [JsonPropertyName("cards")]
    public required List<KanbanCardDto> Cards { get; init; }

    [JsonPropertyName("deck")]
    public required List<FlashcardDto> Deck { get; init; }
}
