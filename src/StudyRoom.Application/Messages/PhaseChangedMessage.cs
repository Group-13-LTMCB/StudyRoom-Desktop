using StudyRoom.Core.Enums;

namespace StudyRoom.Application.Messages;

/// <summary>
/// Phát khi Pomodoro chuyển trạng thái Focus &lt;-&gt; Break.
/// </summary>
public sealed record PhaseChangedMessage(PomodoroPhase NewPhase, int DurationSeconds);
