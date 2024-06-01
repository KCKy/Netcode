using Kcky.GameNewt.Utility;
using SFML.Graphics;
using SFML.Window;
using Clock = SFML.System.Clock;

namespace SfmlExtensions;

/// <summary>
/// Base display framework for a game for SFML rendering framework.
/// Manages the window creation, delta calculation and debug info display.
/// </summary>
public abstract class GameBase
{
    static readonly VideoMode Mode = new(960, 540);
    static readonly Color Background = Color.Black;
    static readonly Font DebugFont = new("LiberationMono-Regular.ttf");

    /// <summary>
    /// The render window of the game.
    /// Draw to this only during <see cref="Draw"/>.
    /// </summary>
    protected readonly RenderWindow Window;
    
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="name">The title of the game window.</param>
    protected GameBase(string name)
    {
        Window = new(Mode, name);
        Window.SetVerticalSyncEnabled(false);
        Window.Closed += (_, _) => Window.Close();
    }

    readonly Clock clock_ = new();

    /// <summary>
    /// Run the game.
    /// </summary>
    public void Run()
    {
        Start();
        while (true)
        {
            if (!Window.IsOpen)
                return;

            Window.DispatchEvents();
            Window.Clear(Background);
            float delta = clock_.ElapsedTime.AsSeconds();
            clock_.Restart();
            Update(delta);
            Draw(delta);
            debugText_.DisplayedString = DebugInfo.Update(delta);
            Window.Draw(debugText_);
            Window.Display();
        }
    }

    /// <summary>
    /// Called when the game is started.
    /// </summary>
    protected virtual void Start() { }

    /// <summary>
    /// The Update call. Should be overloaded with custom drawing code of the game.
    /// </summary>
    /// <param name="delta">The draw delta of this frame.</param>
    protected virtual void Update(float delta) { }

    /// <summary>
    /// The draw call. Should be overloaded with custom drawing code of the game.
    /// </summary>
    /// <param name="delta">The draw delta of this frame.</param>
    protected virtual void Draw(float delta) { }

    readonly Text debugText_ = new()
    {
        Font = DebugFont,
        CharacterSize = 24,
        FillColor = Color.White
    };

    /// <summary>
    /// Keeps track of debug statistics about the client.
    /// </summary>
    protected readonly DebugInfo DebugInfo = new();
}
