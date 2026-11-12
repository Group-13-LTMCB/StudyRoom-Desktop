namespace StudyRoom.Core.Interfaces;

public interface IAmbientSoundService : IDisposable
{
    string CurrentTrack { get; }
    double Volume { get; set; }
    bool IsPlaying { get; }
    void PlayTrack(string trackName);
    void SetVolume(double volume);
    void Stop();
}
