using StudyRoom.Core.Entities;
using StudyRoom.Core.Enums;

namespace StudyRoom.Core.Interfaces;

public interface IPomodoroEngine : IDisposable
{
    PomodoroSession CurrentSession { get; }
    event Action<PomodoroSession>? Tick;
    event Action<PomodoroSession>? PhaseCompleted;

    void SyncFromServer(DateTimeOffset? targetEndTimeUtc, PomodoroPhase phase, TimerState state);
    void Start();
    void Pause();
    void Resume();
    void Reset();
    void SkipPhase();
    void PassBaton(string newHolderUserId);
    bool CanControl(string currentUserId, StudyGroup group);
}
