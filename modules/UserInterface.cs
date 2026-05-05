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
    public enum Alignment
    {
        Start,
        Center,
        End
    }
    public enum LayoutDirection
    {
        Vertical,
        Horizontal
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

        public void CenterOn(IDrawable targetChild)
        {
            // Find the vertical center of the carousel's bounds
            float carouselCenterY = Rect.Height * 0.5f;
            float currentYOffset = Padding.Y;

            foreach (var child in Children)
            {
                if (child == targetChild)
                {
                    // We found the clicked button! 
                    // Calculate the exact _targetScroll needed to align their centers.
                    float childCenterY = currentYOffset + (child.Rect.Height * 0.5f);
                    _targetScroll = carouselCenterY - childCenterY;
                    break;
                }
                // Add height + padding to track the raw Y offset of the next button
                currentYOffset += child.Rect.Height + Padding.Y;
            }
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

    public class Slider : IDrawable
    {
        public Rectangle Rect { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; }
        public float Value { get; set; }

        private int _headWidth;
        private bool _isDragging;
        private MouseState _prevMouseState;

        // Refined constructor: 'bounds' dictates the interactive hit-box and position.
        public Slider(Rectangle bounds, float minValue, float maxValue, float initialValue, int headWidth = 10)
        {
            Rect = bounds;
            MinValue = minValue;
            MaxValue = maxValue;
            Value = MathHelper.Clamp(initialValue, minValue, maxValue);
            _headWidth = headWidth;
            _prevMouseState = Mouse.GetState();
        }

        public void Update(ArtGame engine, float dt)
        {
            MouseState mouseState = Mouse.GetState();

            // 1. Check if the user just clicked inside the slider bounds
            if (mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                if (Rect.Contains(new Point(mouseState.X, mouseState.Y)))
                {
                    _isDragging = true;
                }
            }

            // 2. Stop dragging if the mouse button is released
            if (mouseState.LeftButton == ButtonState.Released)
            {
                _isDragging = false;
            }

            // 3. If actively dragging, calculate the new value based on mouse X position
            if (_isDragging)
            {
                // Normalize the mouse X relative to the slider's width (0.0 to 1.0)
                float percent = (float)(mouseState.X - Rect.X) / Rect.Width;
                percent = MathHelper.Clamp(percent, 0f, 1f);

                // Lerp the value between Min and Max
                Value = MathHelper.Lerp(MinValue, MaxValue, percent);
            }

            _prevMouseState = mouseState;
        }

        public void Draw(ArtGame engine)
        {
            // Calculate percentages to place the head correctly
            float percent = (Value - MinValue) / (MaxValue - MinValue);

            // Draw the track (a thin line centered vertically in the Rect)
            float trackHeight = 4f;
            float trackY = Rect.Y + (Rect.Height / 2f) - (trackHeight / 2f);
            engine.DrawRectangle(Rect.X, trackY, Rect.Width, trackHeight, Color.DarkGray);

        // Calculate the head's X position, offsetting by headWidth so it doesn't spill out of bounds
        float headX = Rect.X + (percent * (Rect.Width - _headWidth));

            // Draw the slider head. Changes color slightly when being actively dragged.
            Color headColor = _isDragging ? Color.LightGray : Color.White;
            engine.DrawRectangle(headX, Rect.Y, _headWidth, Rect.Height, headColor);
        }
    }

    public class ListFrame : IDrawable
    {
        public List<IDrawable> Children { get; set; }
        public float Padding { get; set; }
        public Alignment Alignment { get; set; }
        public Rectangle Rect { get; set; }
        public Color Color { get; set; }
        public LayoutDirection Direction { get; set; }

        public ListFrame(
            Rectangle rect,
            List<IDrawable> children,
            float padding = 5f,
            Alignment alignment = Alignment.Start,
            LayoutDirection layoutDirection = LayoutDirection.Vertical,
            Color color = default)
        {
            Rect = rect;
            Children = children;
            Padding = padding;
            Alignment = alignment;
            Color = color;
            Direction = layoutDirection;

            // Arrange the children immediately upon creation
            PerformLayout();
        }

        // The layout engine: computes the spatial arrangement of the children
        public void PerformLayout()
        {
            // Track the running cursor for both axes
            float currentX = Rect.X;
            float currentY = Rect.Y;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                Rectangle childRect = child.Rect;

                int newX = Rect.X;
                int newY = Rect.Y;

                if (Direction == LayoutDirection.Vertical)
                {
                    // === VERTICAL LAYOUT ===
                    // Stack items down the Y axis
                    newY = (int)currentY;

                    // Align items along the X axis
                    switch (Alignment)
                    {
                        case Alignment.Start:
                            newX = Rect.X;
                            break;
                        case Alignment.Center:
                            newX = Rect.X + (Rect.Width / 2) - (childRect.Width / 2);
                            break;
                        case Alignment.End:
                            newX = (Rect.X + Rect.Width) - childRect.Width;
                            break;
                    }

                    // Advance the Y cursor for the next item
                    currentY += childRect.Height + Padding;
                }
                else if (Direction == LayoutDirection.Horizontal)
                {
                    // === HORIZONTAL LAYOUT ===
                    // Stack items across the X axis
                    newX = (int)currentX;

                    // Align items along the Y axis. 
                    // In a horizontal context: Left = Top, Center = Middle, Right = Bottom
                    switch (Alignment)
                    {
                        case Alignment.Start: // Top aligned
                            newY = Rect.Y;
                            break;
                        case Alignment.Center: // Middle aligned
                            newY = Rect.Y + (Rect.Height / 2) - (childRect.Height / 2);
                            break;
                        case Alignment.End: // Bottom aligned
                            newY = (Rect.Y + Rect.Height) - childRect.Height;
                            break;
                    }

                    // Advance the X cursor for the next item
                    currentX += childRect.Width + Padding;
                }

                // Apply the newly calculated position
                child.Rect = new Rectangle(newX, newY, childRect.Width, childRect.Height);
            }
        }

        public void Update(ArtGame engine, float dt)
        {
            // Optional: If you plan on adding/removing items dynamically or resizing the frame 
            // during gameplay, you should call PerformLayout() here so it constantly recalculates.

            foreach (var child in Children)
                child.Update(engine, dt);
        }

        public void Draw(ArtGame engine)
        {
            engine.DrawRectangle(Rect.X, Rect.Y, Rect.Width, Rect.Height, Color);
            foreach (var child in Children)
                child.Draw(engine);
        }
    }

    public class IconButton : IDrawable
    {
        public Rectangle Rect { get; set; }
        public Texture2D IconTexture { get; set; }
        public Action<IconButton>? OnAction { get; set; }

        public Alignment Alignment { get; set; }
        public float Padding { get; set; }

        // Optional colors to help style the hover effect
        public Color BackgroundColor { get; set; } = Color.Black;
        public Color HoverBackgroundColor { get; set; } = new Color(10, 10, 10);
        public Color IconColor { get; set; } = Color.White;
        public Color HoverIconColor { get; set; } = Color.LightGray;

        private bool _isClicked;
        private bool _isHovered;
        private MouseState _prevMouseState;

        public IconButton(
            Rectangle rect,
            Texture2D iconTexture,
            float padding = 10f,
            Alignment alignment = Alignment.Center,
            Action<IconButton>? onAction = null,
            Color bgColor = default,
            Color hoverBgColor = default)
        {
            Rect = rect;
            IconTexture = iconTexture;
            Padding = padding;
            Alignment = alignment;
            BackgroundColor = bgColor;
            HoverBackgroundColor = hoverBgColor;
            OnAction = onAction;
            _prevMouseState = Mouse.GetState();
        }

        public void Update(ArtGame engine, float dt)
        {
            var mouseState = Mouse.GetState();
            var mousePos = new Point(mouseState.X, mouseState.Y);

            _isHovered = Rect.Contains(mousePos);

            bool isMousePressed = mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released;
            _isClicked = _isHovered && isMousePressed;

            if (_isClicked)
                OnAction?.Invoke(this);

            _prevMouseState = mouseState;
        }

        public void Draw(ArtGame engine)
        {
            // 1. Draw the button background
            Color bgColor = _isHovered ? HoverBackgroundColor : BackgroundColor;
            engine.DrawRectangle(Rect.X, Rect.Y, Rect.Width, Rect.Height, bgColor);

            if (IconTexture == null) return;

            // 2. Calculate the maximum available area for the icon inside the padding
            float availWidth = Rect.Width - (Padding * 2);
            float availHeight = Rect.Height - (Padding * 2);

            // Find the scale required to fit the icon while preserving aspect ratio
            float scale = Math.Min(availWidth / IconTexture.Width, availHeight / IconTexture.Height);

            int drawW = (int)(IconTexture.Width * scale);
            int drawH = (int)(IconTexture.Height * scale);

            // 3. Vertically center the icon. If the height is the limiting factor (as with most buttons), 
            // this naturally creates perfect symmetrical padding on the top and bottom.
            int drawY = Rect.Y + (Rect.Height - drawH) / 2;
            int drawX = Rect.X;

            // 4. Apply horizontal alignment
            switch (Alignment)
            {
                case Alignment.Start:
                    drawX = Rect.X + (int)Padding;
                    break;
                case Alignment.End:
                    drawX = Rect.Right - (int)Padding - drawW;
                    break;
                case Alignment.Center:
                    drawX = Rect.X + (Rect.Width - drawW) / 2;
                    break;
            }

            // 5. Draw the icon
            Color iconTint = _isHovered ? HoverIconColor : IconColor;
            Rectangle destRect = new Rectangle(drawX, drawY, drawW, drawH);

            // Note: Make sure ArtGame's SpriteBatch property is accessible (public or internal) 
            // to draw the raw Texture2D directly like this.
            engine.SpriteBatch.Draw(IconTexture, destRect, iconTint);
        }
    }

    public class SearchBar : IDrawable
    {
        // The core physical bounds
        private Rectangle _baseRect;
        public Rectangle Rect { get; set; }

        public string TextValue { get; set; } = "";
        public string Placeholder { get; set; }
        public string FontName { get; set; }

        public bool IsFocused { get; private set; }

        // Animation Variables
        private float _focusScale = 0f;  // Animates from 0.0 to 1.0
        private float _cursorTimer = 0f;

        private MouseState _prevMouseState;
        private KeyboardState _prevKeyState;

        public SearchBar(Rectangle rect, string fontName, string placeholder = "Search...")
        {
            _baseRect = rect;
            Rect = rect;
            FontName = fontName;
            Placeholder = placeholder;

            _prevMouseState = Mouse.GetState();
            _prevKeyState = Keyboard.GetState();
        }

        // Call this from your Game's Window.TextInput event!
        public void ReceiveTextInput(char character)
        {
            if (!IsFocused) return;

            // Reset cursor blink so it stays solid while actively typing
            _cursorTimer = 0f;

            // If it's a standard printable character, add it!
            if (char.IsLetterOrDigit(character) || char.IsPunctuation(character) || char.IsSymbol(character) || character == ' ')
            {
                TextValue += character;
                Console.WriteLine(TextValue);
            }
        }

        public void Update(ArtGame engine, float dt)
        {
            var mouseState = Mouse.GetState();
            var keyState = Keyboard.GetState();
            Point mousePos = new Point(mouseState.X, mouseState.Y);

            // 1. Handle Click to Focus
            if (mouseState.LeftButton == ButtonState.Pressed && _prevMouseState.LeftButton == ButtonState.Released)
            {
                IsFocused = Rect.Contains(mousePos);
                if (IsFocused)
                {
                    TextInputEXT.StartTextInput();
                    TextInputEXT.TextInput += ReceiveTextInput; // Subscribe to text input events
                } else
                {
                    TextInputEXT.StopTextInput();
                    TextInputEXT.TextInput -= ReceiveTextInput;
                }
            }

            // 2. Animate the Focus State (Smoothly lerp between 0 and 1)
            float targetScale = IsFocused ? 1f : 0f;

            // 15f is the animation speed. Higher = faster snapping.
            _focusScale = MathHelper.Lerp(_focusScale, targetScale, 15f * dt);

            // Expand the rectangle's width slightly when focused
            int expandAmount = (int)(20 * _focusScale);
            Rect = new Rectangle(
                _baseRect.X - (expandAmount / 2),
                _baseRect.Y,
                _baseRect.Width + expandAmount,
                _baseRect.Height
            );

            // 3. Handle Backspace (We handle this manually since TextInput sometimes misses control keys)
            if (IsFocused)
            {
                _cursorTimer += dt;

                // Simple backspace logic. (For holding down backspace, you'd need a repeat timer, but this works for taps)
                if (engine.IsKeyPressed(Keys.Back) && TextValue.Length > 0)
                {
                    TextValue = TextValue.Substring(0, TextValue.Length - 1);
                    _cursorTimer = 0f;
                }
            }

            _prevMouseState = mouseState;
            _prevKeyState = keyState;
        }

        public void Draw(ArtGame engine)
        {
            // 1. Draw Background
            // Darkens slightly when clicked
            Color bgColor = Color.Lerp(new Color(25, 25, 25), new Color(40, 40, 40), _focusScale);
            engine.DrawRectangle(Rect.X, Rect.Y, Rect.Width, Rect.Height, bgColor);

            // 2. Draw Animated Underline
            // Slides out from the center based on the focus animation
            float currentLineWidth = Rect.Width * _focusScale;
            float lineX = Rect.X + (Rect.Width / 2f) - (currentLineWidth / 2f);
            engine.DrawRectangle(lineX, Rect.Bottom - 2, currentLineWidth, 2, Color.CornflowerBlue);

            // 3. Setup Text Alignment
            string textToDraw = string.IsNullOrEmpty(TextValue) ? Placeholder : TextValue;
            Vector2 textSize = engine.MeasureText(FontName, textToDraw, 18f);

            // Vertically center the text
            float textY = Rect.Y + (Rect.Height - textSize.Y) / 2f;
            Vector2 textPos = new Vector2(Rect.X + 10, textY);

            // 4. Draw the Text
            if (string.IsNullOrEmpty(TextValue))
            {
                engine.DrawText(FontName, Placeholder, textPos, Color.Gray * 0.5f, 18f);
            }
            else
            {
                engine.DrawText(FontName, TextValue, textPos, Color.White, 18f);
                Console.WriteLine(TextValue);
            }

            // 5. Draw Blinking Cursor
            if (IsFocused)
            {
                // Modulo math to make it visible for 0.5s, invisible for 0.5s
                if (_cursorTimer % 1.0f < 0.5f)
                {
                    // Measure JUST the real typed text to put the cursor at the very end
                    Vector2 actualTextSize = engine.MeasureText(FontName, TextValue, 1f);
                    float cursorX = Rect.X + 10 + actualTextSize.X + 2; // +2 for a little padding

                    engine.DrawRectangle(cursorX, textY + 2, 2, actualTextSize.Y - 4, Color.White);
                }
            }
        }
    }
}
