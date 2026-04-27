using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using ArtFramework;
using MainGame;

namespace ArtFramework.UserInterface
{
    public interface IDrawable
    {
        Rectangle Rect { get; set; }
        void Update(ArtGame engine, float dt);
        void Draw(ArtGame engine);
    }

    public class ScrollCarrousel : IDrawable
    {
        public Rectangle Rect { get; set; }
        public List<IDrawable> Children { get; set; }
        public Color BackgroundColor { get; set; }
        public Vector2 Padding { get; set; }
        public float ScrollSpeed { get; set; }
        public float CurveMagnitude { get; set; }
        public bool ClipChildren { get; set; }

        private float _scroll;
        private float _targetScroll;

        // FNA specific input tracking
        private MouseState _prevMouse;

        public ScrollCarrousel(
            Rectangle rect,
            List<IDrawable>? children = null,
            Color? backgroundColor = null,
            Vector2 padding = new(),
            bool clipChildren = false,
            float scrollSpeed = 20f,
            float curveMagnitude = 200f)
        {
            Rect = rect;
            Children = children ?? new List<IDrawable>();
            BackgroundColor = backgroundColor ?? Color.DarkGray;
            Padding = padding;
            ClipChildren = clipChildren;
            ScrollSpeed = scrollSpeed;
            CurveMagnitude = curveMagnitude;

            _prevMouse = Mouse.GetState();
        }

        public void Update(ArtGame engine, float dt)
        {
            HandleInput();

            _scroll = MathHelper.Lerp(_scroll, _targetScroll, 8f * dt);
            if (Math.Abs(_scroll - _targetScroll) < 0.1f) _scroll = _targetScroll;

            Layout(engine);

            foreach (var child in Children)
                child.Update(engine, dt);
        }

        public void Draw(ArtGame engine)
        {
            // 1. Draw Background
            engine.DrawRectangle(Rect.X, Rect.Y, Rect.Width, Rect.Height, BackgroundColor);

            bool scissorApplied = false;

            // 2. Begin Scissor Mask (if enabled)
            if (ClipChildren)
            {
                Rectangle screenBounds = new Rectangle(0, 0, engine.ScreenWidth, engine.ScreenHeight);
                Rectangle scissorRect = Rectangle.Intersect(Rect, screenBounds);

                if (scissorRect.Width > 0 && scissorRect.Height > 0)
                {
                    // Intercept batch using reflection (since engine methods are protected)
                    engine.GetType().GetMethod("CloseSpriteBatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(engine, null);

                    engine.GraphicsDevice.ScissorRectangle = scissorRect;
                    engine.OpenScissorBatch(new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None });
                    scissorApplied = true;
                }
            }

            // 3. Draw Children
            foreach (var child in Children)
                child.Draw(engine);

            // 4. End Scissor Mask
            if (scissorApplied)
            {
                engine.GetType().GetMethod("CloseSpriteBatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(engine, null);
                engine.GetType().GetMethod("OpenSpriteBatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(Effect) }, null)?.Invoke(engine, new object[] { null });
            }
        }

        // -------------------------------------------------------------------------

        private void HandleInput()
        {
            var mouse = Mouse.GetState();
            Point mousePos = new Point(mouse.X, mouse.Y);

            if (Rect.Contains(mousePos))
            {
                // XNA Scroll Wheel is cumulative and represented in multiples of 120.
                // We calculate the delta, divide by 120 to normalize to Raylib's -1/1, and multiply by speed.
                int scrollDelta = mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    _targetScroll += (scrollDelta / 120f) * ScrollSpeed;
                }

                // Handle drag scrolling
                if (mouse.LeftButton == ButtonState.Pressed)
                {
                    _targetScroll += mouse.Y - _prevMouse.Y;
                }
            }

            _prevMouse = mouse;
        }

            private void Layout(ArtGame engine)
            {
                float centerY = Rect.Y + Rect.Height * 0.5f;
                float cursorY = Rect.Y + Padding.Y + _scroll;

                // Unused in the final calculation but good to have if you want to clamp later
                // float screenW = engine.ScreenWidth; 

                foreach (var child in Children)
                {
                    // Get the struct, modify it, put it back
                    Rectangle r = child.Rect;

                    // XNA Rectangles require Ints
                    r.Y = (int)cursorY;

                    // Normalised distance from centre [0 = centre, 1 = carousel edge, >1 = beyond].
                    float t = Math.Abs((r.Y + r.Height * 0.5f) - centerY) / (Rect.Height * 0.5f);

                    // Quadratic falloff: 1 at centre, 0 at edge.
                    float curve = MathF.Pow(MathHelper.Clamp(1f - t, 0f, 1f), 2f);

                    // Centre button sits at Rect.X (left edge of the carousel).
                    // Farther buttons slide right by however much curve they have lost.
                    r.X = (int)(((Rect.X + Rect.Width) - r.Width) + CurveMagnitude * (1f - curve));

                    // Reassign the modified rect
                    child.Rect = r;

                    cursorY += r.Height + Padding.Y;
                }
            }
    }
    public class LevelButton : IDrawable
    {
        // ── Layout ────────────────────────────────────────────────────────────────
        public Rectangle Rect { get; set; }

        // ── Content ───────────────────────────────────────────────────────────────
        public string TextureName { get; set; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Version { get; set; } = "";
        public string FontName { get; set; } = "koltav"; // Added so you can customize fonts!

        public Color DifficultyColor { get; set; } = new Color(104, 114, 224, 255);

        // ── Events ────────────────────────────────────────────────────────────────
        public Action<LevelButton>? OnAction { get; set; }

        // ── State ─────────────────────────────────────────────────────────────────
        public bool IsClicked { get; private set; }
        public bool IsHovered { get; private set; }
        private MouseState _prevMouseState;

        // ── Animation ─────────────────────────────────────────────────────────────
        private float _hoverT = 0f;
        private const float HoverSpeed = 10f;

        // ── Constants matching osu!lazer ─────────────────────────────────────────
        private const int AccentWidth = 3;
        private const float DotRadius = 9f;
        private const int TextPadLeft = 16;

        // ── Constructor ───────────────────────────────────────────────────────────
        public LevelButton(
            Action<LevelButton>? onAction,
            Rectangle rect,
            string textureName,
            string title = "",
            string artist = "",
            string version = "",
            Color? difficultyColor = null)
        {
            Rect = rect;
            TextureName = textureName;
            Title = title;
            Artist = artist;
            Version = version;
            DifficultyColor = difficultyColor ?? new Color(104, 114, 224, 255);
            OnAction = onAction;

            // NOTE: Removed `batch?.Add(this)` since DrawBatch wasn't in the engine files, 
            // but you can add it back if you port your DrawBatch manager!
        }

        // ── Update ────────────────────────────────────────────────────────────────
        public void Update(ArtGame engine, float dt)
        {
            var mouseState = Mouse.GetState();
            var mousePos = new Point(mouseState.X, mouseState.Y);

            IsHovered = Rect.Contains(mousePos);

            // Replaces Raylib.IsMouseButtonPressed
            bool isMousePressed = mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            IsClicked = IsHovered && isMousePressed;

            _prevMouseState = mouseState; // Store for next frame

            // Smooth hover lerp
            float target = IsHovered ? 1f : 0f;
            _hoverT = Lerp(_hoverT, target, 1f - MathF.Exp(-HoverSpeed * dt));

            if (IsHovered)
                OnAction?.Invoke(this);
        }

        // ── Draw ─────────────────────────────────────────────────────────────────
        public void Draw(ArtGame engine)
        {
            int x = Rect.X + (int)MainGame.CoreGame2.CarouselOffsetX;
            int y = Rect.Y;
            int w = Rect.Width;
            int h = Rect.Height;

            // ── 1. Cover image (Scissored to card bounds) ────────────────────────
            // Grab the texture to calculate scale
            Texture2D tex = engine.UseTexture(TextureName, "");

            float scaleX = Rect.Width / (float)tex.Width;
            float scaleY = Rect.Height / (float)tex.Height;
            float scale = Math.Max(scaleX, scaleY); // object-fit: cover

            int drawW = (int)(tex.Width * scale);
            int drawH = (int)(tex.Height * scale);
            int drawX = (int)MainGame.CoreGame2.CarouselOffsetX + Rect.X + (Rect.Width - drawW) / 2;
            int drawY = Rect.Y + (Rect.Height - drawH) / 2;

            // Ensure the scissor rectangle doesn't crash FNA by going out of screen bounds
            Rectangle screenBounds = new Rectangle(0, 0, engine.ScreenWidth, engine.ScreenHeight);
            Rectangle scissorRect = Rectangle.Intersect(Rect, screenBounds);

            if (scissorRect.Width > 0 && scissorRect.Height > 0)
            {
                engine.GetType().GetMethod("CloseSpriteBatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(engine, null);

                engine.GraphicsDevice.ScissorRectangle = scissorRect;
                engine.OpenScissorBatch(new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None });

                // Draw the scaled cover
                engine.DrawTexturePro(TextureName,
                    new Rectangle(0, 0, tex.Width, tex.Height),
                    new Rectangle(drawX, drawY, drawW, drawH),
                    Vector2.Zero, 0f, Color.White);

                // Resume standard non-scissored batch for the rest of the UI
                engine.GetType().GetMethod("CloseSpriteBatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(engine, null);
                engine.GetType().GetMethod("OpenSpriteBatch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new Type[] { typeof(Effect) }, null)?.Invoke(engine, new object[] { null });
            }

            // ── 2. Dark gradient overlay ──────────────────────────────────────────
            int gradWidthA = (int)(w * 0.75f);

            DrawRectangleGradientH(engine, x, y, gradWidthA, h,
                new Color(0, 0, 0, 255),
                new Color(0, 0, 0, 0));

            // ── 4. Left accent strip ───────────────────────────────────────────────
            Color accentNormal = DifficultyColor;
            Color accentHovered = LightenColor(DifficultyColor, 40);
            Color accentColor = LerpColor(accentNormal, accentHovered, _hoverT);

            engine.DrawRectangle(x, y, AccentWidth, h, accentColor);

            // ── 5. Difficulty dot on the accent strip ──────────────────────────────
            var dotPos = new Vector2(x + AccentWidth / 2f, y + h / 2f);

            // Solid Yellow Fill
            engine.DrawRing(dotPos, 0f, DotRadius, 0, 360, 16, new Color(255, 228, 78, 255));
            // White Outline (Thickness 1.5)
            engine.DrawRing(dotPos, DotRadius - 1.5f, DotRadius, 0, 360, 16, Color.White);

            // ── 6. Text ───────────────────────────────────────────────────────────
            int textX = x + AccentWidth + TextPadLeft;
            int titleY = y + (int)(h * 0.40f);
            int artistY = y + (int)(h * 0.56f);
            int versionY = y + (int)(h * 0.85f);

            if (!string.IsNullOrEmpty(Title))
                engine.DrawTextPro(FontName, Title, new Vector2(textX, titleY), Vector2.Zero, 0f, 22f, Color.White);

            if (!string.IsNullOrEmpty(Artist))
                engine.DrawTextPro(FontName, Artist, new Vector2(textX, artistY), Vector2.Zero, 0f, 13f, new Color(255, 255, 255, 184));

            if (!string.IsNullOrEmpty(Version))
                engine.DrawTextPro(FontName, Version, new Vector2(textX, versionY), Vector2.Zero, 0f, 11f, LightenColor(DifficultyColor, 60));

            // ── 7. Hover overlay ──────────────────────────────────────────────────
            if (_hoverT > 0.001f)
            {
                byte overlayAlpha = (byte)(_hoverT * 30f);
                engine.DrawRectangle(x, y, w, h, new Color(255, 255, 255, (int)overlayAlpha));
            }

            // ── 8. Card outline (Flat lines to replace RoundedLines) ─────────────
            byte borderAlpha = (byte)(Lerp(20f, 60f, _hoverT));
            Color borderColor = new Color(0, 0, 0, (int)borderAlpha);

            // Draw 4 thin rectangles to make a border
            engine.DrawRectangle(x, y, w, 1, borderColor); // Top
            engine.DrawRectangle(x, y + h - 1, w, 1, borderColor); // Bottom
            engine.DrawRectangle(x, y, 1, h, borderColor); // Left
            engine.DrawRectangle(x + w - 1, y, 1, h, borderColor); // Right
        }

        // ── Custom Draw Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Draws a horizontal gradient efficiently utilizing SpriteBatch deferred rendering.
        /// </summary>
        private void DrawRectangleGradientH(ArtGame engine, int x, int y, int width, int height, Color leftColor, Color rightColor)
        {
            // By drawing 1-pixel wide strips, we simulate Raylib.DrawRectangleGradientH.
            // Since SpriteBatch is Deferred, this is all batched into 1 very fast draw call!
            for (int i = 0; i < width; i++)
            {
                float t = i / (float)(width - 1);
                Color c = LerpColor(leftColor, rightColor, t);
                engine.DrawRectangle(x + i, y, 1, height, c);
            }
        }

        // ── Math Helpers ─────────────────────────────────────────────────────────

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static Color LerpColor(Color a, Color b, float t)
        {
            return new Color(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t),
                (byte)(a.A + (b.A - a.A) * t)
            );
        }

        private static Color LightenColor(Color c, int amount)
        {
            return new Color(
                (byte)Math.Min(c.R + amount, 255),
                (byte)Math.Min(c.G + amount, 255),
                (byte)Math.Min(c.B + amount, 255),
                c.A
            );
        }
    }
}
