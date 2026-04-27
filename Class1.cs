using ArtFramework;
using ArtFramework.UserInterface;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OsuLib;

namespace MainGame;

public class CoreGame2 : ArtGame
{
    // Constants
    private const float LogoSize = 0.3f;
    private const float BeatSpeed = 1500f;
    private const float MaxAngle = 8f;
    private const float RotateDuration = 2f;
    private const float fadeSpeed = 3.7f;    // Adjust for faster/slower fade
    private const int MaxHalos = 5;
    private const float parallaxStrength = 0.03f;

    // Audio Processor State
    public static float currentMuffle = 1.0f;
    public static float targetMuffle = 1.0f;

    // Transition Enums & Structs
    public struct Halo
    {
        public Vector2 position;
        public float radius;
        public float thickness;
        public float alpha;
        public bool active;
    }

    // Game State
    private Halo[] halos = new Halo[MaxHalos];
    private float bgFadeAlpha = 1.0f;
    private bool _hasStartedOnce = false;

    // Screen transition animation
    // _menuT:       1 = main menu fully visible, 0 = fully hidden (faded + shrunk)
    // _carouselSlideT: 0 = carousel off-screen right, 1 = fully in position
    private float _menuT = 1f;
    private float _carouselSlideT = 0f;
    private const float ScreenTransSpeed = 4.5f; // units/sec — lower = slower crossfade
    public static float CarouselOffsetX = 0f;

    // Variables
    private Vector2 offset = Vector2.Zero;
    private float rotation = 0;
    private float timer = 0.0f;
    private float fadetimer = 0.0f;
    private float targetAlpha = 255.0f;
    private float transitionProgress = 0.0f;
    private float transitionSpeed = 1.7f;
    private float transitionlogoSpeed = 0.75f;
    private float logoscale = 5.0f;
    private float volume = 0f;
    private int lastBeatIndex = -1;

    // Music Chart State
    private float previewTime = 0f;
    private string title = string.Empty;
    private string audioFilename = string.Empty;
    private float selectedBeatmapVolume = 0f;
    private string key = string.Empty;

    private bool beatpolar = false;
    private bool swingingRight = true;
    private bool menuvisible = false;
    private byte currentAlpha = 0;

    // Heartbeat Wrap State
    private float heartbeatTrailLength = 800f;
    private float _currentX = 0f;
    private float _drawSpeed = 1900f; // Pixels per second
    private Texture2D? _pixel; // Remember to initialize this as a 1x1 white texture!

    // UI Elements
    private LevelButton? lastHovered = null;
    private ScrollCarrousel? carrousel;
    private string? currentBackground;
    private string? lastBackground;

    // Osu Spesific State
    OsuBeatmap? lastStartingBeatmap = null;
    OsuBeatmap? randomStartingBeatmap = null;

    // Fade variables for level list
    float fadeTimer = 0f;
    float fadeDuration = .5f;

    // Entry Point
    public static void Main()
    {
        Environment.SetEnvironmentVariable("FNA_GRAPHICS_BACKBUFFER_SCALE_NEAREST", "0");
        using var game = new CoreGame2();
        game.Run();
    }

    protected override void Init()
    {
        // ── Window ──────────────────────────────────────────────────────────
        // NOTE: fullscreen is handled in ConfigureWindow, not in constructor.
        // If you want fullscreen, pass true as the last argument.
        ConfigureWindow(1920, 1080, "HopeCore", fullscreen: true);
        SetFPS(80);
        HideCursor();
        AudioEngine();

        // ── Resources ───────────────────────────────────────────────────────
        // Textures loaded directly from file (no content pipeline needed)
        UseTexture("logo", "./media/logo3.png");
        UseTexture("logo_mono", "./media/logo_mono2.png");
        UseTexture("bg", "./media/bg4.jpg");
        UseTexture("heartbeat", "./media/heartbeat4.png");
        UseTexture("maskot", "./media/maskot.png");

        // Sound Effects
        UseSoundEffect("chart_hover", "./sounds/sfxs/default-hover.wav");
        UseSoundEffect("play_click", "./sounds/sfxs/click-short.wav");
        UseSoundEffect("back_click", "./sounds/sfxs/back-button-click.wav");
        UseSoundEffect("chart_click", "./sounds/sfxs/default-select.wav");

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // NOTE: Fonts must be pre-compiled with MGCB.
        // Run:  mgcb Content/mainfont.spritefont
        // Then copy Content/mainfont.xnb to your output folder.
        // The second parameter (path) is ignored for SpriteFont — only the name matters.
        LoadShader();
        LoadAtlasFont("kanit", "./Content/kanit.json", "./Content/kanit.png");
        LoadAtlasFont("koltav", "./Content/koltav.json", "./Content/koltav.png");

        UseMusic("bgm", "./sounds/bgm5.mp3");

        var scanner = new OsuScanner();
        var drawables = new List<ArtFramework.UserInterface.IDrawable>();
        string songsPath = @"C:/Users/YOMARI/Documents/123123/";
        Random rnd = new Random();
        int scannedCount = 0;

        foreach (OsuBeatmap bm in scanner.ScanLazy(songsPath))
        {
            // Use the library's texture helpers to prepare the background
            UseTexture(bm.GetBackground(), bm.GetBackgroundFullPath());
            UseMusic($"{bm.AudioFilename}-{bm.Title}-{bm.PreviewTime}", Path.Combine(Path.GetDirectoryName(bm.FilePath) ?? string.Empty, bm.AudioFilename));

            scannedCount++;
            if (rnd.Next(scannedCount) == 0) // Gives every scanned map an equal chance to be picked
            {
                randomStartingBeatmap = bm;
            }

            // 3. Create a button for each beatmap using the parsed metadata
            var button = new LevelButton(
                    (btn) =>
                    {
                        if (btn.IsClicked)
                        {
                            PlaySoundEffect("chart_click");

                            lastBackground = currentBackground;
                            currentBackground = bm.GetBackground();
                            bgFadeAlpha = 0;
                            fadeTimer = 0f;

                            if (key != $"{bm.AudioFilename}-{bm.Title}-{bm.PreviewTime}")
                            {
                                StopMusic(key);

                                // 2. Update references
                                audioFilename = bm.AudioFilename;
                                title = bm.Title;
                                previewTime = bm.PreviewTime;
                                key = $"{audioFilename}-{title}-{previewTime}";
                                lastStartingBeatmap = randomStartingBeatmap;
                                randomStartingBeatmap = bm;

                                // 3. Start the new song
                                selectedBeatmapVolume = 0;
                                PlayMusic(key);
                                SeekMusic(key, bm.PreviewTime / 1000f);
                            }
                        }
                        else if (lastHovered != btn)
                        {
                            lastHovered = btn;
                            PlaySoundEffect("chart_hover");
                        }
                    },
                    new Rectangle(0, 0, 800, 100),
                    bm.GetBackground(),
                    title: bm.Title,         // Convenience property
                    artist: bm.Artist,       // Convenience property
                    version: bm.Version,     // Convenience property
                    difficultyColor: GetDifficultyColor(float.TryParse(bm.GetDifficulty("OverallDifficulty"), out float rating) ? rating : 0f)
                );

            drawables.Add(button);
        }

        if (randomStartingBeatmap != null)
        {
            currentBackground = randomStartingBeatmap.GetBackground();

            audioFilename = randomStartingBeatmap.AudioFilename;
            title = randomStartingBeatmap.Title;
            previewTime = randomStartingBeatmap.PreviewTime;
            key = $"{audioFilename}-{title}-{previewTime}";

            PlayMusic(key);
            SeekMusic(key, previewTime / 1000f);
            SetMusicVolume(key, selectedBeatmapVolume);
        }

        carrousel = new ScrollCarrousel(
            rect: new Rectangle(1120, 0, 800, 1080),
            children: drawables,
            backgroundColor: new Color(0, 0, 0, 0),
            scrollSpeed: 45,
            curveMagnitude: 150);
    }

    protected override void Update(float dt)
    {
        if (randomStartingBeatmap == null) return;

        float musicTime = GetMusicTimePlayed(key);
        float beatLengthSec = (float)(randomStartingBeatmap.GetTimingPointAt(musicTime * 1000f, uninheritedOnly: true)?.BeatLength / 1000.0 ?? 0.5);
        int currentBeatIndex = (int)(musicTime / beatLengthSec);
        float beatProgress = (musicTime % beatLengthSec) / beatLengthSec;
        float t = timer / RotateDuration;

        timer += dt;
        offset.X += BeatSpeed * dt;
        logoscale = GetBeatEasedValue(beatProgress);

        _currentX += _drawSpeed * dt;

        // Wait until the PEN minus the TAIL LENGTH is completely off the screen!
        if (_currentX - heartbeatTrailLength > ScreenWidth)
        {
            _currentX = -heartbeatTrailLength; // Loop back to the left side
        }

        // Level list volume transition
        if (selectedBeatmapVolume < 0.6f && _hasStartedOnce)
        {
            // 1. Add delta time to our running timer
            fadeTimer += dt;

            // 2. Cap the timer so it doesn't exceed the total duration
            float elapsed = Math.Min(fadeTimer, fadeDuration);

            // 3. Calculate the volume using the easing function
            // t: Current elapsed time
            // b: Starting volume (0f)
            // c: Total change in volume (0.6f - 0f = 0.6f)
            // d: Total duration of the animation
            selectedBeatmapVolume = Easings.EaseCubicInOut(elapsed, 0f, 0.6f, fadeDuration);

            SetMusicVolume(key, selectedBeatmapVolume);
        }
        //else if (selectedBeatmapVolume > 0.0f)
        //{
        //    fadeTimer = 0f;
        //    selectedBeatmapVolume = Math.Max(0.0f, selectedBeatmapVolume - (0.45f * dt));
        //    SetMusicVolume(key, selectedBeatmapVolume);
        //}

        // Level list background transition
        if (bgFadeAlpha < 1.0f)
        {
            bgFadeAlpha = Math.Min(1.0f, bgFadeAlpha + (fadeSpeed * dt));
        }

        if (t > 1.0f) t = 1.5f;

        if (lastStartingBeatmap != null && randomStartingBeatmap != lastStartingBeatmap)
        {
            musicTime = GetMusicTimePlayed(key);
            beatLengthSec = (float)(randomStartingBeatmap.GetTimingPointAt(musicTime * 1000f, uninheritedOnly: true)?.BeatLength / 1000.0 ?? 0.5);
            currentBeatIndex = (int)(musicTime / beatLengthSec);
            Console.WriteLine($"different: {lastStartingBeatmap.Title}, {randomStartingBeatmap.Title}");

            lastStartingBeatmap = randomStartingBeatmap;
            lastBeatIndex = -1; // Reset beat index to trigger beat event immediately on new song
        }
        if (!_hasStartedOnce && musicTime <= (previewTime / 1000f) + 1.5f)
        {
            fadetimer = Math.Min(1.0f, fadetimer + (dt * transitionlogoSpeed));
            currentAlpha = (byte)Easings.EaseCubicInOut(fadetimer, 0.0f, targetAlpha, 1.0f);
        }
        else
        {
            // The intro is officially over! Lock it out so it never runs again.
            _hasStartedOnce = true;

            if (!menuvisible)
            {
                fadetimer = Math.Max(0.0f, fadetimer - dt);
                transitionProgress = Math.Min(3.0f, transitionProgress + (transitionSpeed * dt));
                //selectedBeatmapVolume = Math.Min(0.5f, selectedBeatmapVolume + (0.25f * dt));
                //SetMusicVolume(key, selectedBeatmapVolume);
            }
            else
            {
                //fadetimer = Math.Min(1.0f, fadetimer + dt);
                carrousel?.Update(this, dt);
            }

            currentAlpha = (byte)Easings.EaseCubicInOut(fadetimer, 0.0f, targetAlpha, 1.0f);
        }

        // Drive the crossfade animation toward current menuvisible state
        float animTarget = menuvisible ? 1f : 0f;
        _carouselSlideT = MoveToward(_carouselSlideT, animTarget, (ScreenTransSpeed/3) * dt);
        _menuT = MoveToward(_menuT, 1f - animTarget, ScreenTransSpeed * dt);

        if (menuvisible) ShowCursor(); else HideCursor();

        // Swing Animation
        if (swingingRight)
            rotation = Easings.EaseQuadInOut(timer, MaxAngle, (-MaxAngle) - MaxAngle, RotateDuration);
        else
            rotation = Easings.EaseQuadInOut(timer, -MaxAngle, MaxAngle - (-MaxAngle), RotateDuration);

        if (timer >= RotateDuration)
        {
            timer = 0.0f;
            swingingRight = !swingingRight;
        }

        // Beat Event
        if (currentBeatIndex > lastBeatIndex)
        {
            lastBeatIndex = currentBeatIndex;
            SpawnHalo(new Vector2(ScreenWidth / 2f, ScreenHeight / 2f));
            logoscale = GetBeatEasedValue(0.0f);
            beatpolar = !beatpolar;
        }

        // Input — Space enters level picker, Tab returns to menu
        if (IsKeyPressed(Keys.Space) && !menuvisible && transitionProgress > 2.7f)
        {
            menuvisible = true;
            PlaySoundEffect("play_click");
        }

        if (IsKeyPressed(Keys.Tab) && menuvisible)
        {
            menuvisible = false;
            PlaySoundEffect("back_click");
        }

        UpdateHalos(dt);
    }

    protected override void Draw(float dt)
    {
        ClearScreen(Color.Black);

        // ── Get textures ─────────────────────────────────────────────────────
        Texture2D logo = UseTexture("logo", "");
        Texture2D logoMono = UseTexture("logo_mono", "");
        Texture2D maskot = UseTexture("maskot", "");

        float sW = ScreenWidth;
        float sH = ScreenHeight;

        // ── Eased animation values ────────────────────────────────────────────
        // EaseOutCubic: fast start, smooth stop — feels snappy for UI slides
        float menuEased = EaseOutCubic(_menuT);
        float carouselEased = EaseOutCubic(_carouselSlideT);

        // ── Logo scale: beats drive logoscale, _menuT drives the shrink-on-exit
        // Shrinks toward 0.88× when fading out — subtle but noticeable
        float menuScaleMult = 0.88f + (menuEased * 0.12f);   // 0.88 → 1.0
        float menuAlpha = menuEased;                       // 0 → 1
        byte menuAlphaByte = (byte)(menuAlpha * 255f);

        // ── LAYER 1: Main Menu ────────────────────────────────────────────────
        // Always drawn; fades + shrinks toward 0 when menuvisible=true
        if (menuAlpha > 0.01f)
        {
            MouseState mouse = Mouse.GetState();
            int paddingbgX = (int)(1920 * parallaxStrength);
            int paddingbgY = (int)(1080 * parallaxStrength);
            float offsetX = ((1920 / 2f) - mouse.X) * parallaxStrength;
            float offsetY = ((1080 / 2f) - mouse.Y) * parallaxStrength;

            Rectangle parallaxRect = new Rectangle(
                -paddingbgX + (int)offsetX,
                -paddingbgY + (int)offsetY,
                1920 + (paddingbgX * 2),
                1080 + (paddingbgY * 2)
            );

            byte bgAlpha = (byte)(Math.Clamp(150 / Math.Clamp(logoscale, 1.0f, 1.015f), 0, 150) * menuAlpha);
            DrawCover(currentBackground ?? string.Empty, parallaxRect, new Color(255, 255, 255, bgAlpha));

            float centerY = sH / 2f;
            float centerX = sW / 2f;

            // Heartbeat line — faded by menuAlpha
            float startX = Math.Max(-heartbeatTrailLength / 2, _currentX - heartbeatTrailLength);
            float startYOffset = GetHeartbeatOffset(startX, centerX);
            Vector2 previousPoint = new Vector2(startX, centerY + startYOffset);

            for (float x = startX + 2; x <= _currentX; x += 2)
            {
                float yOffset = GetHeartbeatOffset(x, centerX);
                Vector2 currentPoint = new Vector2(x, centerY + yOffset);

                float distanceFromTail = x - startX;
                float trailAlpha = distanceFromTail / heartbeatTrailLength;
                byte alphaByte = (byte)(255 * trailAlpha * menuAlpha);
                DrawLine(this.SpriteBatch, _pixel!, previousPoint, currentPoint, new Color(112, 193, 255, alphaByte), 18f);

                previousPoint = currentPoint;
            }

            DrawHalos(menuAlpha);

            // Logo — scaled by beat animation × shrink factor
            float scaledLogoSize = LogoSize * logoscale * menuScaleMult;
            var source = new Rectangle(0, 0, logo.Width, logo.Height);
            var dest = new Rectangle((int)(sW / 2f), (int)(sH / 2f),
                                          (int)(logo.Width * scaledLogoSize),
                                          (int)(logo.Height * scaledLogoSize));
            var origin = new Vector2(dest.Width / 2f, dest.Height / 2f);

            float monoSize = LogoSize * menuScaleMult;
            var dest_mono = new Rectangle((int)(sW / 2f), (int)(sH / 2f),
                                           (int)(logoMono.Width * monoSize),
                                           (int)(logoMono.Height * monoSize));
            var origin_mono = new Vector2(dest_mono.Width / 2f, dest_mono.Height / 2f);

            var fadeTint = new Color(255, 255, 255, (int)currentAlpha);
            var maskotTint = new Color(255, 255, 255, menuAlphaByte);

            // "Press [SPACE]" prompt
            Vector2 textSize = MeasureText("koltav", "Press [SPACE] To Start!", 25.0f * logoscale * menuScaleMult);
            Vector2 centerPos = new Vector2(sW / 2f, sH / 1.25f);
            float paddingX = 25f;
            float paddingY = 15f;

            int promptAlpha = (int)(Math.Clamp(255 - currentAlpha * 1.5f, 0, 255) * menuAlpha);
            DrawRectangle(
                centerPos.X - (textSize.X + paddingX) / 2f,
                centerPos.Y - (textSize.Y + paddingY + 3.2f),
                textSize.X + paddingX,
                textSize.Y + paddingY,
                new Color(55, 72, 96, promptAlpha)
            );
            DrawTextPro("koltav", "Press [SPACE] To Start!", centerPos, textSize / 2f, 0.0f,
                25.0f * logoscale * menuScaleMult,
                new Color(162, 215, 255, promptAlpha),
                strokeWidth: 3f,
                strokeColor: new Color(39, 54, 74, promptAlpha));
                
            DrawTexturePro("logo", source, dest, origin, rotation, new Color(255, 255, 255, promptAlpha));
            RenderGridHorizontal(this, transitionProgress, 80, Color.Black, true, true);
            DrawTexturePro("logo_mono", new Rectangle(0, 0, logoMono.Width, logoMono.Height),
                           dest_mono, origin_mono, rotation,
                           new Color(255, 255, 255, (byte)(fadeTint.A * menuAlpha)));
        }

        // ── LAYER 2: Level Picker ─────────────────────────────────────────────
        // Slides in from the right; carousel starts off-screen and moves to its rest position.
        // carouselOffsetX: how many pixels to the right of its final position the carousel starts.
        if (carouselEased > 0.01f)
        {
            ShowCursor();

            MouseState mouse = Mouse.GetState();
            int paddingX = (int)(1920 * parallaxStrength);
            int paddingY = (int)(1080 * parallaxStrength);
            float offsetX = ((1920 / 2f) - mouse.X) * parallaxStrength;
            float offsetY = ((1080 / 2f) - mouse.Y) * parallaxStrength;

            Rectangle parallaxRect = new Rectangle(
                -paddingX + (int)offsetX,
                -paddingY + (int)offsetY,
                1920 + (paddingX * 2),
                1080 + (paddingY * 2)
            );

            byte bgLayerAlpha = (byte)(carouselEased * 150f);

            if (lastBackground != null && bgFadeAlpha < 1.0f)
            {
                byte oldAlpha = (byte)((1.0f - bgFadeAlpha) * bgLayerAlpha);
                DrawCover(lastBackground, parallaxRect, new Color(255, 255, 255, oldAlpha));
            }

            if (currentBackground != null)
            {
                byte newAlpha = (byte)(bgFadeAlpha * bgLayerAlpha);
                DrawCover(currentBackground, parallaxRect, new Color(255, 255, 255, newAlpha));
            }

            // Slide the carousel: offset shrinks from 800px → 0 as carouselEased goes 0 → 1
            float carouselOffsetX = (1f - carouselEased) * 800f;
            CarouselOffsetX = carouselOffsetX;
            //SpriteBatch.End();
            //SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied,
            //                  SamplerState.LinearClamp, null, null, null,
            //                  Matrix.CreateTranslation(carouselOffsetX, 0f, 0f));
            carrousel?.Draw(this);
            //OpenSpriteBatch(); // restore engine's normal batch state
        }

        // Debugging
        DrawTextPro("koltav", $"FrameTime: {GetFrameTime():F4}", new Vector2(20, ScreenHeight - 70), Vector2.Zero, 0.0f, 15.0f,
                new Color(162, 215, 255, 255), strokeWidth: 1.5f, strokeColor: new Color(39, 54, 74, 255));
        DrawTextPro("koltav", $"FPS: {GetFPS()}", new Vector2(20, ScreenHeight - 50), Vector2.Zero, 0.0f, 15.0f,
                new Color(162, 215, 255, 255), strokeWidth: 1.5f, strokeColor: new Color(39, 54, 74, 255));
        DrawTextPro("koltav", "Internal Development v4.22.26(iFNA) [Do not distribute]", new Vector2(20, ScreenHeight - 30), Vector2.Zero, 0.0f, 15.0f,
                new Color(162, 215, 255, 255), strokeWidth: 1.5f, strokeColor: new Color(39, 54, 74, 255));
    }

    // ── Math & Effects ────────────────────────────────────────────────────────

    // Smooth step from a toward b at a fixed speed, never overshooting
    private static float MoveToward(float a, float b, float speed)
    {
        float delta = b - a;
        if (MathF.Abs(delta) <= speed) return b;
        return a + MathF.Sign(delta) * speed;
    }

    // Fast-out easing: snappy start, smooth landing
    private static float EaseOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - MathF.Pow(1f - t, 4f); // Power of 4 (Quartic) is the smoothest!
        //return t == 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
    }

    private float GetBeatEasedValue(float progress)
    {
        progress = Math.Clamp(progress, 0.0f, 1.0f);
        float easedT = 1.0f - ((1.0f - progress) * (1.0f - progress) * (1.0f - progress));
        return 0.95f + (1.0f - 0.95f) * easedT;
    }

    private void SpawnHalo(Vector2 pos)
    {
        for (int i = 0; i < MaxHalos; i++)
        {
            if (!halos[i].active)
            {
                halos[i] = new Halo { position = pos, radius = 20f, thickness = 8f, alpha = 1f, active = true };
                break;
            }
        }
    }

    private void UpdateHalos(float dt)
    {
        for (int i = 0; i < MaxHalos; i++)
        {
            if (!halos[i].active) continue;
            halos[i].radius += 700f * dt;
            halos[i].thickness += 50f * dt;
            halos[i].alpha -= 1.5f * dt;
            if (halos[i].alpha <= 0f) halos[i].active = false;
        }
    }

    private void DrawHalos(float alphaMultiplier = 1f)
    {
        for (int i = 0; i < MaxHalos; i++)
        {
            if (!halos[i].active) continue;

            float a = halos[i].alpha * alphaMultiplier;
            Color glowColor = Fade(new Color(112, 193, 255, 255), a * 0.3f);
            DrawRing(halos[i].position, halos[i].radius - 5f, halos[i].radius + halos[i].thickness + 15f, 0, 360, 64, glowColor);

            Color coreColor = Fade(new Color(162, 215, 255, 255), a);
            DrawRing(halos[i].position, halos[i].radius, halos[i].radius + halos[i].thickness, 0, 360, 64, coreColor);
        }
    }

}