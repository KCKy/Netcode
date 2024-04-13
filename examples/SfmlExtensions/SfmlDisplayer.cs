using Kcky.GameNewt.Client;
using Kcky.GameNewt.Providers;
using Kcky.GameNewt.Utility;
using SFML.Graphics;
using SFML.Window;
using Clock = SFML.System.Clock;

namespace SfmlExtensions;

/// <summary>
/// Base for creating a client <see cref="IDisplayer{T}"/> over the SFML rendering framework.
/// Manages the window creation, delta calculation and debug info display.
/// </summary>
/// <typeparam name="T">The type of the game state.</typeparam>
public abstract class SfmlDisplayer<T> : IDisplayer<T>
{
    static readonly VideoMode Mode = new(960, 540);
    static readonly Color Background = Color.Black;
    static readonly Font DebugFont = new("LiberationMono-Regular.ttf");
    
    /// <summary>
    /// The ID of this client, populated before <see cref="OnInit"/> is invoked.
    /// </summary>
    public long Id = long.MinValue;

    IClient? client_;

    /// <summary>
    /// Reference to the client object, if provided corresponding debug info may be calculated.
    /// </summary>
    public IClient? Client
    {
        get => client_;
        set
        {
            DebugInfo.Client = value;
            client_ = value;
        }
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="name">The title of the game window.</param>
    protected SfmlDisplayer(string name)
    {
        Window = new(Mode, name);
        Window.SetVerticalSyncEnabled(true);
        Window.Closed += (_, _) => Window.Close();
    }

    readonly Clock clock_ = new();

    /// <summary>
    /// Update call invokes drawing, should be called repeatedly in a thread until the window is closed or the game is terminated.
    /// </summary>
    /// <returns>Whether the displayer is still valid. When false calling update is no longer necessary.</returns>
    public bool Update()
    {
        if (!Window.IsOpen)
            return false;

        Window.DispatchEvents();
        Window.Clear(Background);
        float delta = clock_.ElapsedTime.AsSeconds();
        clock_.Restart();
        Draw(delta);
        debugText_.DisplayedString = DebugInfo.Update(delta);
        Window.Draw(debugText_);
        Window.Display();

        return true;
    }

    /// <summary>
    /// The draw call. Should be overloaded with custom drawing code of the game.
    /// </summary>
    /// <param name="delta">The draw delta of this frame.</param>
    protected abstract void Draw(float delta);

    /// <summary>
    /// The window corresponding to this displayer.
    /// </summary>
    public RenderWindow Window { get; }

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

    /// <inheritdoc/>
    public void Init(long id)
    {
        Id = id;
        OnInit();
    }

    /// <summary>
    /// Called when initialization is finished. Should be overloaded with custom post init code.
    /// </summary>
    protected virtual void OnInit() { }

    /// <inheritdoc/>
    public virtual void AddAuthoritative(long frame, T gameState) { }

    /// <inheritdoc/>
    public virtual void AddPredict(long frame, T gameState) { }
}
