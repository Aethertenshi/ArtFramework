using ArtFramework.AtlasParser;
using ManagedBass;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using static System.Net.Mime.MediaTypeNames;

namespace ArtFramework;
public partial class ArtGame
{
    // Caches
    private Dictionary<string, MtsdfFont> _fonts = new();
    private Dictionary<string, Texture2D> _textures = new();
    private Dictionary<string, int> _musics = new();
    private Dictionary<string, int> _sfxs = new();
    private Dictionary<string, Effect> _shaders = new();

    // Texture — loaded directly from file, no content pipeline needed
    public Texture2D UseTexture(string textureName, string texturePath)
    {
        if (!_textures.ContainsKey(textureName))
        {
            using var stream = File.OpenRead(texturePath);
            _textures.Add(textureName, Texture2D.FromStream(GraphicsDevice, stream));
        }
        return _textures[textureName];
    }

    // Music — Bass stays the same, FNA's built-in MediaPlayer is too limited
    public int UseMusic(string musicName, string musicPath)
    {
        if (!_musics.ContainsKey(musicName))
        {
            int handle = Bass.CreateStream(musicPath, 0, 0, BassFlags.Default);

            if (handle == 0)
                Console.WriteLine($"Failed to load {musicName}: {Bass.LastError}");

            _musics.Add(musicName, handle);
        }
        return _musics[musicName];
    }
    public int UseSoundEffect(string soundName, string soundPath)
    {
        if (!_sfxs.ContainsKey(soundName))
        {
            // 1. Switch to SampleLoad. 
            // Set 'max' (the 4th param) to 16 or 32 to allow that many overlapping sounds.
            int handle = Bass.SampleLoad(soundPath, 0, 0, 32, BassFlags.Default);

            if (handle == 0)
                Console.WriteLine($"Failed to load sample {soundName}: {Bass.LastError}");

            _sfxs.Add(soundName, handle);
        }

        // 2. Return the Sample Handle
        return _sfxs[soundName];
    }

    // Shader — FNA uses compiled .fxb Effect files
    public Effect UseShader(string shaderName, string fxbPath)
    {
        if (!_shaders.ContainsKey(shaderName))
        {
            using var stream = File.OpenRead(fxbPath);
            _shaders.Add(shaderName, new Effect(GraphicsDevice, ReadBytes(stream)));
        }
        return _shaders[shaderName];
    }

    // Cleanup — call this when your game exits
    public void UnloadResources()
    {
        foreach (var t in _textures.Values) t.Dispose();
        foreach (var s in _shaders.Values) s.Dispose();
        foreach (var m in _musics.Values) Bass.StreamFree(m);
        _textures.Clear();
        _fonts.Clear();
        _musics.Clear();
        _shaders.Clear();
    }

    private static byte[] ReadBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}