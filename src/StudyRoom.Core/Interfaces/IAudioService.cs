namespace StudyRoom.Core.Interfaces;

public interface IAudioService
{
    bool IsAudioEnabled { get; set; }
    void PlayFocusStart();
    void PlayBreakStart();
    void PlayMessageNotification();
    void PlayCelebration();
}
