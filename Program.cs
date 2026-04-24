using ArtFramework;
using ArtFramework.UserInterface;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OsuLib;

namespace MainGame;

public class CoreGame : ArtGame
{
    // Constants
    private const float LogoSize = 0.3f;
    private const float BeatSpeed = 1500f;
    private const float BeatInterval = 0f;
    private const float MaxAngle = 8f;
    private const float RotateDuration = 2f;
    private const float fadeSpeed = 2.0f;    // Adjust for faster/slower fade
    private const int MaxHalos = 5;

    // Audio Processor State
    private static float previousSampleLeft = 0.0f;
    private static float previousSampleRight = 0.0f;
    public static float currentMuffle = 1.0f;
    public static float targetMuffle = 1.0f;

    // Transition Enums & Structs
    public enum MainTransitionState { INACTIVE = 0, FADING_IN, WAITING, FADING_OUT }

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
    private MainTransitionState transState = MainTransitionState.INACTIVE;
    private float transTimer = 0.0f;
    private float transWaitDuration = 0.0f;
    private float transFadeSpeed = 0.4f;
    private float bgFadeAlpha = 1.0f; // 0.0 to 1.0
    private bool transHasSwappedBG = false;

    // Variables
    private Vector2 offset = Vector2.Zero;
    private Vector2 size;
    private float rotation = 0;
    private float timer = 0.0f;
    private float fadetimer = 0.0f;
    private float currentCooldown = 0.0f;
    private float targetAlpha = 255.0f;
    private float transitionProgress = 0.0f;
    private float transitionSpeed = 1.7f;
    private float transitionlogoSpeed = 0.75f;
    private float logoscale = 5.0f;
    private float beatLengthSec = 506.7210958154872f / 1000.0f; //315.7894736842105f / 1000.0f; //350.94117647058823f / 1000.0f;
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

    // Entry Point
    public static void Main()
    {
        Environment.SetEnvironmentVariable("FNA_GRAPHICS_BACKBUFFER_SCALE_NEAREST", "0");
        using var game = new CoreGame();
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

        //UseFont("koltav", fontSize: 64);
        //UseFont("kanit", fontSize: 64);

        UseMusic("bgm", "./sounds/bgm5.mp3");

        var scanner = new OsuScanner();
        var drawables = new List<ArtFramework.UserInterface.IDrawable>();
        string songsPath = @"C:/Users/YOMARI/Documents/123123/";

        foreach (OsuBeatmap bm in scanner.ScanLazy(songsPath))
        {
            // Use the library's texture helpers to prepare the background
            UseTexture(bm.GetBackground(), bm.GetBackgroundFullPath());
            UseMusic($"{bm.AudioFilename}-{bm.Title}-{bm.PreviewTime}", Path.Combine(Path.GetDirectoryName(bm.FilePath), bm.AudioFilename));

            if (string.IsNullOrEmpty(audioFilename))
            {
                currentBackground = bm.GetBackground();

                audioFilename = bm.AudioFilename;
                title = bm.Title;
                previewTime = bm.PreviewTime;
                key = $"{audioFilename}-{title}-{previewTime}";

                PlayMusic(key);
                SeekMusic(key, bm.PreviewTime / 1000f);
                SetMusicVolume(key, selectedBeatmapVolume);
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

                        if (key != $"{bm.AudioFilename}-{bm.Title}-{bm.PreviewTime}")
                        {
                            StopMusic(key);

                            // 2. Update references
                            audioFilename = bm.AudioFilename;
                            title = bm.Title;
                            previewTime = bm.PreviewTime;
                            key = $"{audioFilename}-{title}-{previewTime}";

                            // 3. Start the new song
                            selectedBeatmapVolume = 0;
                            PlayMusic(key);
                            SeekMusic(key, bm.PreviewTime / 1000f);
                        }
                    }
                    else if (lastHovered != btn) {
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

        carrousel = new ScrollCarrousel(
            rect: new Rectangle(1120, 0, 800, 1080),
            children: drawables,
            backgroundColor: new Color(0, 0, 0, 0),
            scrollSpeed: 45,
            curveMagnitude: 150);

        size = new Vector2(350, ScreenHeight);

        SetMusicVolume("bgm", volume);
        PlayMusic("bgm");
        SeekMusic("bgm", 0.0f);
    }

    protected override void Update(float dt)
    {
        float musicTime = GetMusicTimePlayed("bgm");
        int currentBeatIndex = (int)(musicTime / beatLengthSec);
        float startOfCurrentBeat = currentBeatIndex * beatLengthSec;
        float timeSinceBeatHit = musicTime - startOfCurrentBeat;
        float beatProgress = timeSinceBeatHit / beatLengthSec;
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
        if (selectedBeatmapVolume < 0.6f && menuvisible)
        {
            selectedBeatmapVolume = Math.Min(0.6f, selectedBeatmapVolume + (0.45f * dt));
            SetMusicVolume(key, selectedBeatmapVolume);
        }
        else if (!menuvisible && selectedBeatmapVolume > 0.0f)
        {
            selectedBeatmapVolume = Math.Max(0.0f, selectedBeatmapVolume - (0.45f * dt));
            SetMusicVolume(key, selectedBeatmapVolume);
        }

        // Level list background transition
        if (bgFadeAlpha < 1.0f)
        {
            bgFadeAlpha = Math.Min(1.0f, bgFadeAlpha + (fadeSpeed * dt));
        }

        // Loop the music
        if (musicTime >= GetMusicLength("bgm"))
        {
            PlayMusic("bgm");
            SeekMusic("bgm", 1.1001f);
            lastBeatIndex = -1;
            musicTime = GetMusicTimePlayed("bgm");
            currentBeatIndex = (int)(musicTime / beatLengthSec);
        }

        if (t > 1.0f) t = 1.5f;

        if (musicTime > 1.1f)
        {
            if (fadetimer < 0.0f) fadetimer = 0.0f;

            if (transState == MainTransitionState.WAITING && !transHasSwappedBG)
            {
                menuvisible = !menuvisible;
                transHasSwappedBG = true;
            }

            if (!menuvisible)
            {
                fadetimer = Math.Max(0.0f, fadetimer - dt);
                transitionProgress = Math.Min(3.0f, transitionProgress + (transitionSpeed * dt));
                volume = Math.Min(0.5f, volume + (0.25f * dt));
            }
            else
            {
                fadetimer = Math.Min(1.0f, fadetimer + dt);
                volume = Math.Max(0.0f, volume - (0.25f * dt));

                carrousel.Update(this, dt);
            }

            currentAlpha = (byte)Easings.EaseCubicInOut(fadetimer, 0.0f, targetAlpha, 1.0f);
        }
        else
        {
            fadetimer = Math.Min(1.0f, fadetimer + (dt * transitionlogoSpeed));
            currentAlpha = (byte)Easings.EaseCubicInOut(fadetimer, 0.0f, targetAlpha, 1.0f);
        }

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

        // Input
        if (IsKeyPressed(Keys.Space) && !menuvisible && transitionProgress > 2.7f)
        {
            transState = MainTransitionState.FADING_IN;
            transFadeSpeed = transitionSpeed / 2f;
            transWaitDuration = 0.25f;

            PlaySoundEffect("play_click");
        }

        if (IsKeyPressed(Keys.Tab) && menuvisible && transState == MainTransitionState.INACTIVE)
        {
            transState = MainTransitionState.FADING_IN;
            transFadeSpeed = transitionSpeed / 2f;
            transWaitDuration = 0.25f;

            PlaySoundEffect("back_click");
        }

        SetMusicVolume("bgm", volume);
        UpdateHalos(dt);
        UpdateTransition(dt);
    }

    protected override void Draw(float dt)
    {
        ClearScreen(Color.Black);

        // ── Get textures ─────────────────────────────────────────────────────
        Texture2D logo = UseTexture("logo", "");
        Texture2D logoMono = UseTexture("logo_mono", "");
        Texture2D bg = UseTexture("bg", "");
        Texture2D heartbeat = UseTexture("heartbeat", "");
        Texture2D maskot = UseTexture("maskot", "");

        float sW = ScreenWidth;
        float sH = ScreenHeight;

        // ── Build rects ───────────────────────────────────────────────────────
        var source = new Rectangle(0, 0, logo.Width, logo.Height);
        var dest = new Rectangle((int)(sW / 2f), (int)(sH / 2f), (int)(logo.Width * (LogoSize * logoscale)), (int)(logo.Height * (LogoSize * logoscale)));
        var origin = new Vector2(dest.Width / 2f, dest.Height / 2f);

        var dest_mono = new Rectangle((int)(sW / 2f), (int)(sH / 2f), (int)(logoMono.Width * LogoSize), (int)(logoMono.Height * LogoSize));
        var origin_mono = new Vector2(dest_mono.Width / 2f, dest_mono.Height / 2f);

        var maskot_source = new Rectangle(0, 0, maskot.Width, maskot.Height);
        var maskot_dest = new Rectangle((int)(sW / 2f), (int)(sH / 2f), (int)((maskot.Width * 0.18f) * logoscale), (int)((maskot.Height * 0.18f) * logoscale));
        var maskot_origin = new Vector2(maskot_dest.Width / 2f, maskot_dest.Height / 2f);

        var bg_origin = new Vector2(bg.Width * 1.15f / 2f, bg.Height * 1.15f / 2f);
        var bg_src = new Rectangle(0, 0, bg.Width, bg.Height);
        var bg_dest = new Rectangle((int)(sW / 2f), (int)(sH / 2f), (int)(bg.Width * 1.15f), (int)(bg.Height * 1.15f));

        var hb_origin = new Vector2((heartbeat.Width + 100) / 2f, (heartbeat.Height + 100) / 2f);
        var hb_src = new Rectangle(0, 0, heartbeat.Width, heartbeat.Height);
        var hb_dest = new Rectangle((int)(sW / 2f), (int)(sH / 2f), heartbeat.Width + 100, heartbeat.Height + 100);

        Vector2 textSize = MeasureText("koltav", "Press [SPACE] To Start!", 25.0f * logoscale);
        // NOTE: Raylib Color(r,g,b,a) with floats uses 0-255 range.
        // FNA Color(float,float,float,float) uses 0-1 range — byte alpha divided by 255.
        var fadeTint = new Color(255, 255, 255, (int)currentAlpha);

        if (!menuvisible)
        {
            byte bgAlpha = (byte)Math.Clamp(255 / Math.Clamp(logoscale, 1.0f, 1.015f), 0, 255);
            // NOTE: FNA float Color ctor is 0-1, so divide bgAlpha by 255
            DrawTexturePro("bg", bg_src, bg_dest, bg_origin, 0.0f, new Color(0.8f, 0.8f, 0.8f, bgAlpha / 255f));

            float centerY = ScreenHeight / 2f;
            float centerX = ScreenWidth / 2f;

            // 1. Calculate where the tail should start (don't go below 0)
            float startX = Math.Max(-heartbeatTrailLength/2, _currentX - heartbeatTrailLength);

            // 2. Set the initial previousPoint to the start of the trail, NOT 0
            float startYOffset = GetHeartbeatOffset(startX, centerX);
            Vector2 previousPoint = new Vector2(startX, centerY + startYOffset);

            // 3. Draw only the segment between startX and the current PenX
            for (float x = startX + 2; x <= _currentX; x += 2)
            {
                float yOffset = GetHeartbeatOffset(x, centerX);
                Vector2 currentPoint = new Vector2(x, centerY + yOffset);

                // --- Bonus: Fading Tail Effect ---
                // Calculate how close this segment is to the end of the trail to fade it out
                float distanceFromTail = x - startX;
                float alpha = distanceFromTail / heartbeatTrailLength;

                // Premultiply the color so it fades out cleanly
                byte alphaByte = (byte)(255 * alpha);
                Color segmentColor = new Color(112, 193, 255, alphaByte);
                // ---------------------------------

                DrawLine(this.SpriteBatch, _pixel, previousPoint, currentPoint, segmentColor, 18f);

                previousPoint = currentPoint;
            }

            DrawHalos();
            DrawTexturePro("logo", source, dest, origin, rotation, Color.White);

            Vector2 centerPos = new Vector2(sW / 2f, sH / 1.25f);
            float paddingX = 25f;
            float paddingY = 15f;

            // 2. Draw Background Rectangle (Slightly Larger than Text, Centered on the Same Point)
            DrawRectangle(
                centerPos.X - (textSize.X + paddingX) / 2f,
                centerPos.Y - (textSize.Y + paddingY + 3.2f),
                textSize.X + paddingX,
                textSize.Y + paddingY,
                new Color(55, 72, 96, (int)Math.Clamp(255 - currentAlpha * 1.5f, 0, 255))
            );

            // 3. Draw Text (Positioned exactly at Center)
            DrawTextPro(
                "koltav",
                "Press [SPACE] To Start!",
                centerPos,             // Use the exact same centerPos
                textSize / 2f,       // Origin is half of text size to center it on pos
                0.0f,
                25.0f * logoscale,
                new Color(162, 215, 255, (int)Math.Clamp(255 - currentAlpha * 1.5f, 0, 255)),
                strokeWidth: 3f,
                strokeColor: new Color(39, 54, 74, (int)Math.Clamp(255 - currentAlpha * 1.5f, 0, 255))
            );

            //RenderGrid(this, transitionProgress, 80, Color.Black, true, true);
            RenderGridHorizontal(this, transitionProgress, 80, Color.Black, true, true);
            DrawTexturePro("logo_mono", source, dest_mono, origin_mono, rotation, fadeTint);
            HideCursor();
        }
        else
        {
            ShowCursor();
            //if (currentBackground != null) DrawCover(currentBackground, new Rectangle(0, 0, 1920, 1080), new Color(255, 255, 255, 100));
            if (lastBackground != null && bgFadeAlpha < 1.0f)
            {
                byte oldAlpha = (byte)((1.0f - bgFadeAlpha) * 100);
                DrawCover(lastBackground, new Rectangle(0, 0, 1920, 1080), new Color(255, 255, 255, oldAlpha));
            }
            if (currentBackground != null)
            {
                // We multiply by 100 here if you want your max opacity to be 100 like your original code
                // If you want full brightness, use 255.
                byte newAlpha = (byte)(bgFadeAlpha * 100);
                DrawCover(currentBackground, new Rectangle(0, 0, 1920, 1080), new Color(255, 255, 255, newAlpha));
            }
            carrousel?.Draw(this);
        }

        DrawTransitionOverlay(maskot_source, maskot_dest, maskot_origin);

        // Debugging
        DrawTextPro("koltav", $"FrameTime: {GetFrameTime():F4}", new Vector2(20, ScreenHeight - 70), Vector2.Zero, 0.0f, 15.0f,
                new Color(162, 215, 255, (int)Math.Clamp(255, 0, 255)),
                strokeWidth: 1.5f,
                strokeColor: new Color(39, 54, 74, (int)Math.Clamp(255, 0, 255)));
        DrawTextPro("koltav", $"FPS: {GetFPS()}", new Vector2(20, ScreenHeight - 50), Vector2.Zero, 0.0f, 15.0f,
                new Color(162, 215, 255, (int)Math.Clamp(255, 0, 255)),
                strokeWidth: 1.5f,
                strokeColor: new Color(39, 54, 74, (int)Math.Clamp(255, 0, 255)));
        DrawTextPro("koltav", "Internal Development v4.22.26(iFNA) [Do not distribute]", new Vector2(20, ScreenHeight - 30), Vector2.Zero, 0.0f, 15.0f,
                new Color(162, 215, 255, (int)Math.Clamp(255, 0, 255)),
                strokeWidth: 1.5f,
                strokeColor: new Color(39, 54, 74, (int)Math.Clamp(255, 0, 255)));
    }

    // ── Math & Effects ────────────────────────────────────────────────────────

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

    private void DrawHalos()
    {
        for (int i = 0; i < MaxHalos; i++)
        {
            if (!halos[i].active) continue;

            // Replaces Raylib.Fade — use our static Fade helper
            Color glowColor = Fade(new Color(112, 193, 255, 255), halos[i].alpha * 0.3f);
            DrawRing(halos[i].position, halos[i].radius - 5f, halos[i].radius + halos[i].thickness + 15f, 0, 360, 64, glowColor);

            Color coreColor = Fade(new Color(162, 215, 255, 255), halos[i].alpha);
            DrawRing(halos[i].position, halos[i].radius, halos[i].radius + halos[i].thickness, 0, 360, 64, coreColor);
        }
    }

    private void UpdateTransition(float dt)
    {
        if (transState == MainTransitionState.INACTIVE) return;
        transTimer += dt;
    }

    private void DrawTransitionOverlay(Rectangle maskot_source, Rectangle maskot_dest, Vector2 maskot_origin)
    {
        if (transState == MainTransitionState.INACTIVE) return;

        float t = Math.Min(transTimer / transFadeSpeed, 1.0f);
        float gridProgress = t * 1.5f;
        float currentAlphaF = 255.0f;

        if (transState == MainTransitionState.FADING_IN)
            currentAlphaF = Easings.EaseCubicInOut(t, 0.0f, 255.0f, 1.2f);
        else if (transState == MainTransitionState.FADING_OUT)
            currentAlphaF = Easings.EaseCubicInOut(1.0f - t, 0.0f, 255.0f, 1.0f);

        byte alphaByte = (byte)Math.Clamp(currentAlphaF, 0, 255);

        const string loadingText = "Stella:\nPlaying in Offline Mode! Please wait~";
        const float fontSize = 22.0f;

        Vector2 connectingSize = MeasureText("kanit", loadingText, fontSize);
        Vector2 textPos = new Vector2(ScreenWidth / 2f, ScreenHeight / 1.25f);
        Vector2 textOrigin = new Vector2(connectingSize.X / 2.0f, connectingSize.Y / 2.0f);

        var bgColor = new Color(70, 103, 141, 255);
        var mascotTint = new Color(255, 255, 255, (int)alphaByte);
        var textTint = new Color(179, 193, 218, (int)alphaByte);

        switch (transState)
        {
            case MainTransitionState.FADING_IN:
                RenderGridHorizontal(this, gridProgress, 80, bgColor, false, true);
                if (transTimer >= transFadeSpeed)
                {
                    transState = MainTransitionState.WAITING;
                    transTimer = 0.0f;
                }
                break;

            case MainTransitionState.WAITING:
                DrawRectangle(0, 0, ScreenWidth, ScreenHeight, bgColor);
                transHasSwappedBG = true;
                if (transTimer >= transWaitDuration)
                {
                    transState = MainTransitionState.FADING_OUT;
                    transTimer = 0.0f;
                }
                break;

            case MainTransitionState.FADING_OUT:
                RenderGridHorizontal(this, gridProgress, 80, bgColor, true, true);
                transHasSwappedBG = false;
                if (transTimer >= transFadeSpeed)
                {
                    transState = MainTransitionState.INACTIVE;
                    transTimer = 0.0f;
                }
                break;
        }

        DrawTexturePro("maskot", maskot_source, maskot_dest, maskot_origin, rotation, mascotTint);
        DrawTextPro("kanit", loadingText, textPos, textOrigin, 0.0f, fontSize, textTint, 3f, new Color(39, 54, 74, (int)alphaByte));
    }
}