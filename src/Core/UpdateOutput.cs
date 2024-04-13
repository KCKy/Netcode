namespace Kcky.GameNewt;

/// <summary>
/// Result of a single state update.
/// Provides a way for the game code to manage the server.
/// </summary>
public struct UpdateOutput
{
    /// <summary>
    /// All ids of clients whose connection will be terminated after this frame update.
    /// </summary>
    public long[]? ClientsToTerminate;

    /// <summary>
    /// Whether this frame update was the last and the server shall stop.
    /// </summary>
    public bool ShallStop;

    /// <summary>
    /// Default constructor. Creates an instance representing no special action.
    /// </summary>
    public UpdateOutput()
    {
        ClientsToTerminate = null;
        ShallStop = false;
    }

    /// <summary>
    /// Empty output, signals no special behaviour.
    /// </summary>
    public static readonly UpdateOutput Empty = new();
}
