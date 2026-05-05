using ArtFramework.AtlasParser;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using Microsoft.Xna.Framework.Input;
using System.Runtime.InteropServices;
using System.Text.Json;
using SDL3;

namespace ArtFramework;

public abstract partial class ArtGame : Game
{
    // Constant
    private const int TARGET_DISPLAY_INDEX = 0;

    // Read Only
    private static readonly (float Rating, Color Color)[] OsuDifficultySpectrum = new[]
    {
        (0.10f, new Color(66, 144, 251)),   // #4290FB (Gray-Blue)
        (1.25f, new Color(79, 192, 255)),   // #4FC0FF (Blue)
        (2.00f, new Color(79, 255, 213)),   // #4FFFD5 (Cyan)
        (2.50f, new Color(124, 255, 79)),   // #7CFF4F (Green)
        (3.30f, new Color(246, 240, 92)),   // #F6F05C (Yellow)
        (4.20f, new Color(255, 128, 104)),  // #FF8068 (Orange/Salmon)
        (4.90f, new Color(255, 78, 111)),   // #FF4E6F (Red)
        (5.80f, new Color(198, 69, 184)),   // #C645B8 (Magenta)
        (6.70f, new Color(101, 99, 222)),   // #6563DE (Purple)
        (7.70f, new Color(24, 21, 142)),    // #18158E (Dark Blue)
        (9.00f, new Color(0, 0, 0))         // #000000 (Black)
    };

    // Internal state
    private Texture2D _pixel = null!;
    private BasicEffect _basicEffect = null!;
    private KeyboardState _prevKeyState;
    private KeyboardState _currKeyState;
    private bool _spriteBatchOpen = false;
    private float _lastDelta = 0f;
    private int _frameCounter = 0;
    private double _timeAccumulator = 0;
    private int _currentFps = 0;
    private Effect _mtsdfEffect;

    // Shader location maps
    private Dictionary<string, Dictionary<string, int>> _shaderParamIds = new();
    private Dictionary<string, Dictionary<int, string>> _shaderParamNames = new();

    // Font base sizes (for scaling)
    private Dictionary<string, int> _fontBaseSizes = new();

    // Called from Initialize()
    private void InitDrawing()
    {
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _basicEffect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            View = Matrix.Identity,
            World = Matrix.Identity
        };
    }

    // ── Window ──────────────────────────────────────────────────────────────

    public void ConfigureWindow(int width, int height, string title, bool fullscreen = false)
    {
        Graphics.PreferredBackBufferWidth = width;
        Graphics.PreferredBackBufferHeight = height;
        Graphics.IsFullScreen = fullscreen;
        Graphics.ApplyChanges();
        Window.Title = title;
    }

    public void SetFPS(int fps)
    {
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / fps);
        IsFixedTimeStep = true;
        Graphics.SynchronizeWithVerticalRetrace = false;
        Graphics.ApplyChanges();
    }

    private void MoveToDisplay(int displayIndex)
    {
        IntPtr displayIdsPtr = SDL.SDL_GetDisplays(out int displayCount);
        if (displayIdsPtr == IntPtr.Zero || displayIndex >= displayCount) return;

        uint displayId = (uint)Marshal.ReadInt32(displayIdsPtr, displayIndex * sizeof(int));
        SDL.SDL_free(displayIdsPtr);

        SDL.SDL_GetDisplayBounds(displayId, out SDL.SDL_Rect bounds);
        SDL.SDL_SetWindowPosition(
            Window.Handle,
            bounds.x + (bounds.w - GraphicsDevice.Viewport.Width) / 2,
            bounds.y + (bounds.h - GraphicsDevice.Viewport.Height) / 2
        );
    }

    public void HideCursor() => IsMouseVisible = false;
    public void ShowCursor() => IsMouseVisible = true;

    public int ScreenWidth => GraphicsDevice.Viewport.Width;
    public int ScreenHeight => GraphicsDevice.Viewport.Height;

    public float GetFrameTime() => _lastDelta;
    public int GetFPS() => _currentFps;

    // ── Input ────────────────────────────────────────────────────────────────

    public bool IsKeyPressed(Keys key) => _currKeyState.IsKeyDown(key) && _prevKeyState.IsKeyUp(key);
    public bool IsKeyDown(Keys key) => _currKeyState.IsKeyDown(key);
    public bool IsKeyReleased(Keys key) => _currKeyState.IsKeyUp(key) && _prevKeyState.IsKeyDown(key);

    // ── Screen ───────────────────────────────────────────────────────────────

    public void ClearScreen(Color color) => GraphicsDevice.Clear(color);

    // ── SpriteBatch management ───────────────────────────────────────────────
    // These are called internally so you never have to manage SpriteBatch manually.

    public void OpenScissorBatch(RasterizerState scissorState)
    {
        // Adjust the BlendState/SamplerState to match whatever your normal OpenSpriteBatch() uses!
        SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            scissorState // <-- This is the magic parameter
        );
        _spriteBatchOpen = true;
    }

    protected void OpenSpriteBatch(Effect? effect = null)
    {
        if (_spriteBatchOpen) SpriteBatch.End();
        SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, null, null, effect);
        _spriteBatchOpen = true;
    }

    protected void CloseSpriteBatch()
    {
        if (!_spriteBatchOpen) return;
        SpriteBatch.End();
        _spriteBatchOpen = false;
    }

    // ── Textures ─────────────────────────────────────────────────────────────

    /// <summary>Raylib-style DrawTexturePro: dest.XY is where origin lands on screen.</summary>
    public void DrawTexturePro(string textureName, Rectangle source, Rectangle dest, Vector2 origin, float rotation, Color tint)
    {
        var texture = _textures[textureName];
        var scale = new Vector2(dest.Width / (float)source.Width, dest.Height / (float)source.Height);
        var srcOrigin = new Vector2(origin.X / scale.X, origin.Y / scale.Y);

        SpriteBatch.Draw(
            texture,
            new Vector2(dest.X, dest.Y),
            source,
            tint,
            MathHelper.ToRadians(rotation),
            srcOrigin,
            scale,
            SpriteEffects.None,
            0f);
    }
    public void DrawCover(string textureName, Rectangle destRec, Color? tint = null)
    {
        // Make sure we have the texture loaded
        if (!_textures.TryGetValue(textureName, out Texture2D texture)) return;

        Color finalTint = tint ?? Color.White;

        // Cast to floats for math so we don't lose decimal precision
        float targetAspect = (float)destRec.Width / destRec.Height;
        float imageAspect = (float)texture.Width / texture.Height;

        float srcX = 0f;
        float srcY = 0f;
        float srcW = texture.Width;
        float srcH = texture.Height;

        if (imageAspect > targetAspect)
        {
            srcW = texture.Height * targetAspect;
            srcX = (texture.Width - srcW) / 2.0f;
        }
        else
        {
            srcH = texture.Width / targetAspect;
            srcY = (texture.Height - srcH) / 2.0f;
        }

        // XNA/FNA Rectangles require integers, so we cast the final math results
        Rectangle sourceRec = new Rectangle((int)srcX, (int)srcY, (int)srcW, (int)srcH);

        // XNA maps the source rectangle perfectly to the destination rectangle. 
        // No origin offset needed!
        SpriteBatch.Draw(texture, destRec, sourceRec, finalTint);
    }

    // ── Text ─────────────────────────────────────────────────────────────────

    // NOTE: UseFont now takes only the content-pipeline name (no file path).
    // Compile your .spritefont files with MGCB and load them by name.
    //public new SpriteFont UseFont(string fontName, string _ = "", int fontSize = 14)
    //{
    //    if (!_fonts.ContainsKey(fontName))
    //    {
    //        _fonts.Add(fontName, Content.Load<SpriteFont>(fontName));
    //    }
    //    _fontBaseSizes[fontName] = fontSize;
    //    return _fonts[fontName];
    //}

    // For MSDF fonts, you must provide the JSON and PNG paths directly, since they aren't part of the content pipeline.
    private MtsdfFont LoadMtsdfFont(string jsonPath, string texturePath)
    {
        var jsonContent = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        var font = new MtsdfFont
        {
            Texture = Texture2D.FromStream(GraphicsDevice, File.OpenRead(texturePath)),
            DistanceRange = root.GetProperty("atlas").GetProperty("distanceRange").GetSingle(),
            EmSize = root.GetProperty("atlas").GetProperty("size").GetSingle()
        };

        foreach (var glyphElement in root.GetProperty("glyphs").EnumerateArray())
        {
            char c = (char)glyphElement.GetProperty("unicode").GetInt32();
            var glyph = new MtsdfGlyph { Advance = glyphElement.GetProperty("advance").GetSingle() };

            if (glyphElement.TryGetProperty("atlasBounds", out var ab))
                glyph.AtlasBounds = new Vector4(ab.GetProperty("left").GetSingle(), ab.GetProperty("bottom").GetSingle(), ab.GetProperty("right").GetSingle(), ab.GetProperty("top").GetSingle());

            if (glyphElement.TryGetProperty("planeBounds", out var pb))
                glyph.PlaneBounds = new Vector4(pb.GetProperty("left").GetSingle(), pb.GetProperty("bottom").GetSingle(), pb.GetProperty("right").GetSingle(), pb.GetProperty("top").GetSingle());

            font.Glyphs[c] = glyph;
        }

        return font;
    }
    public void LoadShader()
    {
        // Load the pre-compiled bytecode
        byte[] bytecode = File.ReadAllBytes("shaders/atlas.fxb");

        // Create the Effect object directly
        _mtsdfEffect = new Effect(GraphicsDevice, bytecode);
    }
    public void LoadAtlasFont(string fontName, string jsonPath, string texturePath)
    {
        _fonts.Add(fontName, LoadMtsdfFont(jsonPath, texturePath));
    }
    public void SetEffectParameters(string fontName)
    {
        // These names must match the names in the .fx file exactly
        var font = _fonts[fontName];
        _mtsdfEffect.Parameters["atlasSize"].SetValue(new Vector2(font.Texture.Width, font.Texture.Height));
        _mtsdfEffect.Parameters["pxRange"].SetValue(font.DistanceRange);
    }

    // This is the core MSDF rendering method. It takes care of all the math to map from glyph metrics to screen positions, and sets the necessary shader parameters for correct rendering.
    public void DrawMtsdfString(
        MtsdfFont font, string text, Vector2 position, Color color,
        float rotation, Vector2 origin, float scale,
        SpriteEffects effects, float layerDepth)
    {
        float cos = (float)Math.Cos(rotation);
        float sin = (float)Math.Sin(rotation);
        Vector2 cursor = Vector2.Zero;

        // 1. Calculate the real texture scale factor
        float textureScale = scale / font.EmSize;

        // 2. Convert the MSDF distance range (padding) into EM units
        float padding = font.DistanceRange / font.EmSize;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                cursor.X = 0f;
                cursor.Y += 1f; // 1 EM unit down
                continue;
            }

            var glyph = font.GetGlyph(c);

            if (glyph.AtlasBounds != Vector4.Zero)
            {
                // 3. Subtract padding so the physical texture aligns with the visual vector bounds
                Vector2 localPos = new Vector2(
                    cursor.X + glyph.PlaneBounds.X - padding,
                   cursor.Y - glyph.PlaneBounds.W - padding   // Y-up → Y-down
                );

                Vector2 offset = (localPos * scale) - origin;
                Vector2 drawPos = position + new Vector2(
                    offset.X * cos - offset.Y * sin,
                    offset.X * sin + offset.Y * cos
                );

                Rectangle src = new Rectangle(
                    (int)glyph.AtlasBounds.X,
                    font.Texture.Height - (int)glyph.AtlasBounds.W,
                    (int)(glyph.AtlasBounds.Z - glyph.AtlasBounds.X),
                    (int)(glyph.AtlasBounds.W - glyph.AtlasBounds.Y)
                );

                // Draw using the new textureScale
                SpriteBatch.Draw(font.Texture, drawPos, src, color,
                    rotation, Vector2.Zero, textureScale, effects, layerDepth);
            }

            cursor.X += glyph.Advance; // EM units
        }
    }

    public void DrawText(string fontName, string text, Vector2 position, Color color, float scale = 1f)
    {
        if (!_fonts.TryGetValue(fontName, out var font)) return;
        CloseSpriteBatch();
        SetEffectParameters(fontName);
        OpenSpriteBatch(_mtsdfEffect);
        DrawMtsdfString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        CloseSpriteBatch();
        OpenSpriteBatch();
    }

    public void DrawTextPro(
        string fontName,
        string text,
        Vector2 position,
        Vector2 origin,     // screen pixels, pass MeasureText()/2 to center
        float rotation,
        float scale,
        Color color,
        float strokeWidth = 0f,
        Color? strokeColor = null)
    {
        if (!_fonts.TryGetValue(fontName, out var font)) return;


        CloseSpriteBatch();
        SetEffectParameters(fontName);
        OpenSpriteBatch(_mtsdfEffect);

        if (strokeWidth > 0f && strokeColor.HasValue)
        {
            float r = MathHelper.ToRadians(rotation);
            Vector2[] offsets = {
            new(-strokeWidth,  0),           new(strokeWidth,  0),
            new(0,            -strokeWidth),  new(0,            strokeWidth),
            new(-strokeWidth, -strokeWidth),  new(strokeWidth, -strokeWidth),
            new(-strokeWidth,  strokeWidth),  new(strokeWidth,  strokeWidth)
        };
            foreach (var off in offsets)
                DrawMtsdfString(font, text, position + off, strokeColor.Value, r, origin, scale, SpriteEffects.None, 0f);
        }

        DrawMtsdfString(font, text, position, color, MathHelper.ToRadians(rotation), origin, scale, SpriteEffects.None, 0f);

        CloseSpriteBatch();
        OpenSpriteBatch();
    }

    //public Vector2 MeasureTextEx(string fontName, string text, float fontSize, float spacing)
    //{
    //    if (!_fonts.TryGetValue(fontName, out var font))
    //    {
    //        Console.WriteLine($"WARNING: Font '{fontName}' not found!"); // Add this to debug!
    //        return Vector2.Zero;
    //    }

    //    float baseSize = _fontBaseSizes.TryGetValue(fontName, out int bs) ? bs : 14f;
    //    font.Spacing = spacing;

    //    var raw = font.MeasureString(text);
    //    float scale = fontSize / baseSize;
    //    return new Vector2(raw.X * scale, raw.Y * scale);
    //}
    public Vector2 MeasureText(string fontName, string text, float scale = 1f)
    {
        if (!_fonts.TryGetValue(fontName, out var font)) return Vector2.Zero;
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        float maxX = 0f, curX = 0f;
        int lines = 1;

        foreach (char c in text)
        {
            if (c == '\n')
            {
                maxX = Math.Max(maxX, curX);
                curX = 0f;
                lines++;
                continue;
            }

            // Safely get the glyph. If it's a missing space, manually add a typical space width (0.25 EM)
            if (font.Glyphs.TryGetValue(c, out var glyph))
            {
                curX += glyph.Advance;
            }
            else if (c == ' ')
            {
                curX += 0.25f; // Standard space width in EM units
            }
        }

        return new Vector2(Math.Max(maxX, curX), lines * 1f) * scale;
    }


    // ── Shapes ───────────────────────────────────────────────────────────────

    public void DrawRectangle(float x, float y, float width, float height, Color color)
        => SpriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);

    /// <summary>Draws a hollow ring. Briefly closes SpriteBatch to use raw vertices.</summary>
    public void DrawRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color)
    {
        CloseSpriteBatch();

        float startRad = MathHelper.ToRadians(startAngle);
        float endRad = MathHelper.ToRadians(endAngle);
        float step = (endRad - startRad) / segments;

        var verts = new VertexPositionColor[segments * 6];
        int idx = 0;

        for (int i = 0; i < segments; i++)
        {
            float a0 = startRad + i * step;
            float a1 = a0 + step;

            var io = new Vector3(center.X + MathF.Cos(a0) * innerRadius, center.Y + MathF.Sin(a0) * innerRadius, 0);
            var oo = new Vector3(center.X + MathF.Cos(a0) * outerRadius, center.Y + MathF.Sin(a0) * outerRadius, 0);
            var i1 = new Vector3(center.X + MathF.Cos(a1) * innerRadius, center.Y + MathF.Sin(a1) * innerRadius, 0);
            var o1 = new Vector3(center.X + MathF.Cos(a1) * outerRadius, center.Y + MathF.Sin(a1) * outerRadius, 0);

            // First triangle
            verts[idx++] = new VertexPositionColor(oo, color);
            verts[idx++] = new VertexPositionColor(io, color);
            verts[idx++] = new VertexPositionColor(o1, color);

            // Second triangle
            verts[idx++] = new VertexPositionColor(o1, color);
            verts[idx++] = new VertexPositionColor(io, color);
            verts[idx++] = new VertexPositionColor(i1, color);
        }

        // 1. Crucial Effect Setups
        _basicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, ScreenWidth, ScreenHeight, 0, 0, 1);
        _basicEffect.VertexColorEnabled = true; // MUST be true to render the 'color' parameter

        // 2. Crucial State Management 
        // Save the old state so we don't break whatever SpriteBatch expects later
        RasterizerState oldRasterizerState = GraphicsDevice.RasterizerState;

        // Disable culling so winding order (CW vs CCW) doesn't matter
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        foreach (var pass in _basicEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, segments * 2);
        }

        // 3. Restore the state
        GraphicsDevice.RasterizerState = oldRasterizerState;

        OpenSpriteBatch();
    }
    public void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 point1, Vector2 point2, Color color, float thickness = 4f)
    {
        float distance = Vector2.Distance(point1, point2);
        float angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);

        spriteBatch.Draw(
            pixel,
            point1,
            null,
            color,
            angle,
            new Vector2(0, 0.5f), // Origin at the left-middle of the pixel
            new Vector2(distance, thickness), // Scale it to the length and thickness
            SpriteEffects.None,
            0
        );
    }

    // ── Shaders ──────────────────────────────────────────────────────────────
    // NOTE: FNA uses pre-compiled HLSL .fxb effects, not GLSL.
    // You must rewrite your .fs shaders as HLSL and compile them with fxc or mgfxc.

    public int GetShaderLocation(string shaderName, string paramName)
    {
        if (!_shaderParamIds.ContainsKey(shaderName))
        {
            _shaderParamIds[shaderName] = new();
            _shaderParamNames[shaderName] = new();
        }

        var ids = _shaderParamIds[shaderName];
        if (!ids.ContainsKey(paramName))
        {
            int id = ids.Count;
            ids[paramName] = id;
            _shaderParamNames[shaderName][id] = paramName;
        }

        return ids[paramName];
    }

    // Overloads — no type enum needed, compiler picks the right one
    public void SetShaderValue(string shaderName, int loc, float value)
        => _shaders[shaderName].Parameters[_shaderParamNames[shaderName][loc]].SetValue(value);

    public void SetShaderValue(string shaderName, int loc, Vector2 value)
        => _shaders[shaderName].Parameters[_shaderParamNames[shaderName][loc]].SetValue(value);

    public void SetShaderValue(string shaderName, int loc, Vector3 value)
        => _shaders[shaderName].Parameters[_shaderParamNames[shaderName][loc]].SetValue(value);

    public void BeginShader(string shaderName)
    {
        CloseSpriteBatch();
        OpenSpriteBatch(_shaders[shaderName]);
    }

    public void EndShader()
    {
        CloseSpriteBatch();
        OpenSpriteBatch();
    }

    // ── Grid Effect ──────────────────────────────────────────────────────────

    /// <summary>Animates a grid of rectangles filling/clearing the screen.</summary>
    public void RenderGrid(ArtGame game, float transitionProgress, int tileSize, Color transitionColor, bool fadeOut, bool reverseWave)
    {
        if (tileSize <= 0) tileSize = 50;

        // Pull screen dimensions from the passed game instance
        int cols = game.ScreenWidth / tileSize + (game.ScreenWidth % tileSize != 0 ? 1 : 0);
        int rows = game.ScreenHeight / tileSize + (game.ScreenHeight % tileSize != 0 ? 1 : 0);

        float maxDist = (cols - 1) + (rows - 1);
        float fadeSpread = 0.4f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float distX = reverseWave ? x : (cols - 1) - x;
                float distY = reverseWave ? (rows - 1) - y : y;

                float totalDist = distX + distY;
                float delayStart = totalDist / maxDist;

                // MathHelper is the XNA equivalent of standard System.Math for game logic
                float tileLocalProgress = MathHelper.Clamp((transitionProgress - delayStart) / fadeSpread, 0.0f, 1.0f);
                float tileAlpha = fadeOut ? 1.0f - tileLocalProgress : tileLocalProgress;

                if (tileAlpha > 0.0f)
                {
                    // XNA's Color struct overloads the '*' operator, making fading very easy.
                    byte alphaByte = (byte)(255 * tileAlpha);
                    Color drawColor = new Color(transitionColor.R, transitionColor.G, transitionColor.B, alphaByte);

                    // Use the custom DrawRectangle method from the game instance
                    game.DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, drawColor);
                }
            }
        }
    }
    public void RenderGridHorizontal(ArtGame game, float transitionProgress, int tileSize, Color transitionColor, bool fadeOut, bool reverseWave)
    {
        if (tileSize <= 0) tileSize = 50;

        // Pull screen dimensions from the passed game instance
        int cols = game.ScreenWidth / tileSize + (game.ScreenWidth % tileSize != 0 ? 1 : 0);
        int rows = game.ScreenHeight / tileSize + (game.ScreenHeight % tileSize != 0 ? 1 : 0);

        float maxDist = (cols - 1) + (rows - 1);
        float fadeSpread = 0.4f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float distX = reverseWave ? x : (cols - 1) - x;

                float totalDist = distX;
                float delayStart = totalDist / maxDist;

                // MathHelper is the XNA equivalent of standard System.Math for game logic
                float tileLocalProgress = MathHelper.Clamp((transitionProgress - delayStart) / fadeSpread, 0.0f, 1.0f);
                float tileAlpha = fadeOut ? 1.0f - tileLocalProgress : tileLocalProgress;

                if (tileAlpha > 0.0f)
                {
                    // XNA's Color struct overloads the '*' operator, making fading very easy.
                    byte alphaByte = (byte)(255 * tileAlpha);
                    Color drawColor = new Color(transitionColor.R, transitionColor.G, transitionColor.B, alphaByte);

                    // Use the custom DrawRectangle method from the game instance
                    game.DrawRectangle(x * tileSize, y * tileSize, tileSize, tileSize, drawColor);
                }
            }
        }
    }

    // ── Color Helpers ─────────────────────────────────────────────────────────

    /// <summary>Equivalent of Raylib.Fade — returns the same color with a new alpha.</summary>
    public static Color Fade(Color color, float alpha)
        => new Color(color.R, color.G, color.B, (byte)Math.Clamp(alpha * 255f, 0, 255));

    public static Color GetDifficultyColor(float rating)
    {
        // Clamp to minimum bounds
        if (rating <= OsuDifficultySpectrum[0].Rating)
            return OsuDifficultySpectrum[0].Color;

        // Clamp to maximum bounds
        if (rating >= OsuDifficultySpectrum[^1].Rating)
            return OsuDifficultySpectrum[^1].Color;

        // Find the correct bracket and interpolate between the two colors
        for (int i = 0; i < OsuDifficultySpectrum.Length - 1; i++)
        {
            var start = OsuDifficultySpectrum[i];
            var end = OsuDifficultySpectrum[i + 1];

            if (rating >= start.Rating && rating <= end.Rating)
            {
                // Calculate how far along we are between the two star ratings (0.0 to 1.0)
                float t = (rating - start.Rating) / (end.Rating - start.Rating);
                return LerpColor(start.Color, end.Color, t);
            }
        }

        return OsuDifficultySpectrum[0].Color; // Fallback
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t)
        );
    }

    // ── Math Helpers ─────────────────────────────────────────────────────────
    protected float GetHeartbeatOffset(float x, float centerX)
    {
        // Divide the raw distance by our width multiplier!
        // If width is 2.0, an actual screen distance of 60 pixels is mathematically treated as 30.
        float dist = (x - centerX) / 5;

        float offset = 0;

        if (dist > -30 && dist <= -15) offset = MathHelper.Lerp(0, 20, (dist + 30) / 15f);
        else if (dist > -15 && dist <= -5) offset = MathHelper.Lerp(20, -150, (dist + 15) / 10f);
        else if (dist > -5 && dist <= 10) offset = MathHelper.Lerp(-150, 100, (dist + 5) / 15f);
        else if (dist > 10 && dist <= 20) offset = MathHelper.Lerp(100, -20, (dist - 10) / 10f);
        else if (dist > 20 && dist <= 30) offset = MathHelper.Lerp(-20, 0, (dist - 20) / 10f);

        return offset * 2.5f;
    }
}