using System.Media;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.UI.Services;

public class WpfAudioService : IAudioService
{
    public bool IsAudioEnabled { get; set; } = true;

    public void PlayFocusStart()
    {
        if (!IsAudioEnabled) return;
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch
        {
            // Headless / non-audio environment fallback
        }
    }

    public void PlayBreakStart()
    {
        if (!IsAudioEnabled) return;
        try
        {
            SystemSounds.Exclamation.Play();
        }
        catch
        {
            // Headless / non-audio environment fallback
        }
    }

    public void PlayMessageNotification()
    {
        if (!IsAudioEnabled) return;
        try
        {
            SystemSounds.Beep.Play();
        }
        catch
        {
            // Headless / non-audio environment fallback
        }
    }

    public void PlayCelebration()
    {
        if (!IsAudioEnabled) return;
        try
        {
            SystemSounds.Hand.Play();
        }
        catch
        {
            // Headless / non-audio environment fallback
        }
    }
}
