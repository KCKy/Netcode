﻿namespace Core;

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
    /// Default constructor. Creates an instance representing nothing.
    /// </summary>
    public UpdateOutput()
    {
        ClientsToTerminate = null;
        ShallStop = false;
    }

    public static readonly UpdateOutput Empty = new();
}
