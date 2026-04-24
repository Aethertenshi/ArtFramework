using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Reflection;

namespace ArtFramework;

public abstract partial class ArtGame : Game
{
    protected GraphicsDeviceManager Graphics;
    protected SpriteBatch SpriteBatch = null!;
    protected ArtGame()
    {
        Graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        
    }

    protected override void Initialize()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        InitDrawing(); // wires up _pixel, _basicEffect
        Init();
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {   
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _lastDelta = delta;
        _timeAccumulator += delta;
        if (_timeAccumulator >= 1.0)
        {
            _currentFps = _frameCounter;
            _frameCounter = 0;

            // ACCURACY TRICK: Subtract exactly 1.0 instead of setting to 0.
            // This preserves fractional milliseconds so your timer doesn't drift!
            _timeAccumulator -= 1.0;
        }

        // Track keyboard state for IsKeyPressed / IsKeyReleased
        _prevKeyState = _currKeyState;
        _currKeyState = Keyboard.GetState();

        if (_currKeyState.IsKeyDown(Keys.Escape)) Exit();

        // Call user-defined Update
        Update(delta);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _frameCounter++;

        OpenSpriteBatch();          // auto-open SpriteBatch before your Draw
        Draw(delta);
        CloseSpriteBatch();         // auto-close after

        base.Draw(gameTime);
    }

    protected override void OnExiting(object sender, EventArgs args)
    {
        AudioCleanup();
        UnloadResources();
        base.OnExiting(sender, args);
    }

    // ── Abstract interface ────────────────────────────────────────────────────

    protected abstract void Init();
    protected abstract void Update(float delta);
    protected abstract void Draw(float delta);
}