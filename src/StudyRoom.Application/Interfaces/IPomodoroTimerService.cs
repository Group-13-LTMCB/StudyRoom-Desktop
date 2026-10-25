using StudyRoom.Core.Entities;

namespace StudyRoom.Application.Interfaces;

public interface IPomodoroTimerService
{
    PomodoroSession CurrentSession { get; }

    event Action<PomodoroSession>? Tick;
    event Action<PomodoroSession>? PhaseCompleted;

    void Start();
    void Pause();
    void Resume();
    void Reset();
    void Skip();
    void PassBaton(Guid newHolderUserId);
}
