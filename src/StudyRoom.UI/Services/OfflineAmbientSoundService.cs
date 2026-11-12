using System.IO;
using System.Media;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.UI.Services;

public class OfflineAmbientSoundService : IAmbientSoundService
{
    private SoundPlayer? _player;
    private MemoryStream? _currentStream;
    private readonly object _lock = new();

    public string CurrentTrack { get; private set; } = "Off";
    public double Volume { get; set; } = 0.5;
    public bool IsPlaying => CurrentTrack != "Off" && CurrentTrack != "Tắt";

    public void PlayTrack(string trackName)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(trackName) || trackName.Equals("Off", StringComparison.OrdinalIgnoreCase) || trackName.Equals("Tắt", StringComparison.OrdinalIgnoreCase))
            {
                Stop();
                return;
            }

            CurrentTrack = trackName;
            ApplyTrackAudio();
        }
    }

    public void SetVolume(double volume)
    {
        lock (_lock)
        {
            Volume = Math.Clamp(volume, 0.0, 1.0);
            if (IsPlaying)
            {
                ApplyTrackAudio();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            try
            {
                _player?.Stop();
                _player?.Dispose();
                _player = null;
                _currentStream?.Dispose();
                _currentStream = null;
            }
            catch
            {
                // Headless fallback
            }
            CurrentTrack = "Off";
        }
    }

    private void ApplyTrackAudio()
    {
        try
        {
            _player?.Stop();
            _player?.Dispose();
            _currentStream?.Dispose();

            if (Volume <= 0.001)
            {
                _player = null;
                _currentStream = null;
                return;
            }

            var wavBytes = GenerateWavBytes(CurrentTrack, Volume);
            _currentStream = new MemoryStream(wavBytes);
            _player = new SoundPlayer(_currentStream);
            _player.PlayLooping();
        }
        catch
        {
            // Headless / non-audio environment fallback
        }
    }

    public static byte[] GenerateWavBytes(string track, double volume)
    {
        const int sampleRate = 22050;
        const int durationSeconds = 4;
        const int numSamples = sampleRate * durationSeconds;
        short[] samples = new short[numSamples];

        var rng = new Random(42); // deterministic seed for clean looping

        float lastSample = 0f;
        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            float s = 0f;

            switch (track.ToLowerInvariant())
            {
                case "rain":
                case "mưa rơi":
                    // Low-pass filtered pink/brown noise with soft droplet clicks
                    lastSample = 0.92f * lastSample + 0.08f * white;
                    s = lastSample * 1.8f;
                    if (rng.NextDouble() < 0.004)
                    {
                        s += (float)(rng.NextDouble() * 0.4 - 0.2);
                    }
                    break;

                case "waves":
                case "sóng biển":
                    // Low-frequency noise modulated by a smooth sinusoidal swell
                    lastSample = 0.96f * lastSample + 0.04f * white;
                    float wavePeriod = 4.0f;
                    float swell = 0.25f + 0.75f * (float)Math.Pow((Math.Sin(2.0 * Math.PI * t / wavePeriod) + 1.0) / 2.0, 2.0);
                    s = lastSample * swell * 2.5f;
                    break;

                case "cafe":
                case "quán cà phê":
                    // Warm bandpass murmur
                    lastSample = 0.88f * lastSample + 0.12f * white;
                    float hum = 0.15f * (float)Math.Sin(2.0 * Math.PI * 120.0 * t);
                    s = (lastSample * 1.2f) + hum;
                    break;

                case "fire":
                case "lửa trại":
                    // Warm low rumble + occasional crackle pops
                    lastSample = 0.95f * lastSample + 0.05f * white;
                    s = lastSample * 1.5f;
                    if (rng.NextDouble() < 0.001)
                    {
                        s += (float)(rng.NextDouble() * 0.8 - 0.4);
                    }
                    break;

                default:
                    s = white * 0.1f;
                    break;
            }

            // Apply master volume and clamp
            float scaled = s * (float)volume;
            scaled = Math.Clamp(scaled, -1.0f, 1.0f);
            samples[i] = (short)(scaled * 32767);
        }

        // Generate 44-byte WAV header + PCM 16-bit mono data
        byte[] wav = new byte[44 + numSamples * 2];
        using (var ms = new MemoryStream(wav))
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + numSamples * 2);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16); // Subchunk1Size
            bw.Write((short)1); // AudioFormat = 1 (PCM)
            bw.Write((short)1); // NumChannels = 1 (Mono)
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2); // ByteRate
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(numSamples * 2);
            for (int i = 0; i < numSamples; i++)
            {
                bw.Write(samples[i]);
            }
        }

        return wav;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
