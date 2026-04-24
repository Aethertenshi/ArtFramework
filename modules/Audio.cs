using ManagedBass;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace ArtFramework;

public abstract partial class ArtGame : Game
{
    // Caches
    private List<int> _audioCallbacks = new();

    // Initializes the audio engine.
    public void AudioEngine()
    {
        if (!Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero))
            Console.WriteLine($"BASS Init Error: {Bass.LastError}");
    }

    public void AudioCleanup()
    {
        foreach (var handle in _musics.Values)
            Bass.StreamFree(handle);

        Bass.Free();
    }

    public void PlayMusic(string musicName, bool restart = true)
    {
        if (_musics.TryGetValue(musicName, out int handle))
            Bass.ChannelPlay(handle, restart);
    }

    public void PlaySoundEffect(string soundName)
    {
        if (_sfxs.TryGetValue(soundName, out int handle))
        {
            int channel = Bass.SampleGetChannel(handle);
            Bass.ChannelPlay(channel);
        }
    }

    public void PauseMusic(string musicName)
    {
        if (_musics.TryGetValue(musicName, out int handle))
            Bass.ChannelPause(handle);
    }

    public void StopMusic(string musicName)
    {
        if (_musics.TryGetValue(musicName, out int handle))
            Bass.ChannelStop(handle);
    }

    public void SetMusicVolume(string musicName, float volume)
    {
        if (_musics.TryGetValue(musicName, out int handle))
            Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, volume);
    }

    public float GetMusicLength(string musicName)
    {
        if (_musics.TryGetValue(musicName, out int handle))
        {
            long byteLength = Bass.ChannelGetLength(handle);
            return (float)Bass.ChannelBytes2Seconds(handle, byteLength);
        }
        return 0f;
    }

    public float GetMusicTimePlayed(string musicName)
    {
        if (_musics.TryGetValue(musicName, out int handle))
        {
            long bytePosition = Bass.ChannelGetPosition(handle);
            return (float)Bass.ChannelBytes2Seconds(handle, bytePosition);
        }
        return 0f;
    }

    public void SeekMusic(string musicName, float position)
    {
        if (_musics.TryGetValue(musicName, out int handle))
        {
            long bytePosition = Bass.ChannelSeconds2Bytes(handle, position);
            Bass.ChannelSetPosition(handle, bytePosition);
        }
    }
}