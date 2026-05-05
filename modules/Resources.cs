using ArtFramework.AtlasParser;
using ManagedBass;
using ManagedBass.Fx;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    public void DeleteMusic(string musicName)
    {
        if (_musics.ContainsKey(musicName))
        {
            _musics.Remove(musicName);
        }
    }
    public int UseMusic(string musicName, string musicPath)
    {
        if (!_musics.ContainsKey(musicName))
        {
            // 1. Create a DECODING stream (required for BASS_FX)
            int decoder = Bass.CreateStream(musicPath, 0, 0, BassFlags.Decode);

            if (decoder == 0)
            {
                Console.WriteLine($"Failed to load decoder for {musicName}: {Bass.LastError}");
                return 0;
            }

            // 2. Create the Tempo stream from the decoder
            // BassFlags.FxFreeSource ensures that when we free the tempo stream, the decoder is freed too
            int tempoHandle = BassFx.TempoCreate(decoder, BassFlags.FxFreeSource);

            if (tempoHandle == 0)
            {
                Console.WriteLine($"Failed to create tempo stream for {musicName}: {Bass.LastError}");
                Bass.StreamFree(decoder);
                return 0;
            }

            _musics.Add(musicName, tempoHandle);
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
    public void SetBlurEffectParameters(Effect blurEffect, float dx, float dy, float blurAmount)
    {
        int kernelSize = 15;
        Vector2[] offsets = new Vector2[kernelSize];
        float[] weights = new float[kernelSize];

        // Calculate weights using Gaussian formula
        float sigma = blurAmount;
        float totalWeight = 0.0f;

        for (int i = 0; i < kernelSize; i++)
        {
            // Distance from the center pixel (center is index 7)
            float distance = i - 7;

            // Apply Gaussian formula
            weights[i] = (float)((1.0 / Math.Sqrt(2 * Math.PI * sigma * sigma)) * Math.Exp(-(distance * distance) / (2 * sigma * sigma)));

            totalWeight += weights[i];
        }

        // Normalize weights so they sum to exactly 1.0 (prevents the image from brightening or darkening)
        for (int i = 0; i < kernelSize; i++)
        {
            weights[i] /= totalWeight;
            // Apply the direction vector (dx, dy) and distance
            offsets[i] = new Vector2(dx * (i - 7), dy * (i - 7));
        }

        blurEffect.Parameters["SampleWeights"].SetValue(weights);
        blurEffect.Parameters["SampleOffsets"].SetValue(offsets);
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