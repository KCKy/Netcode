﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FrameworkTest;

public sealed class MockServerSession : IServerSession
{
    long sessionId_ = 1;
    readonly object sessionIdMutex_ = new();

    readonly Dictionary<long, Api> idToSession_ = new();

    internal long ConnectMock(Api session)
    {
        Monitor.Enter(sessionIdMutex_);
        long id = sessionId_++;

        lock (idToSession_)
            idToSession_.Add(id, session);
        
        Monitor.Exit(sessionIdMutex_);

        session.OnConnect += () => OnClientConnect?.Invoke(id);
        session.OnDisconnect += () => OnClientConnect?.Invoke(id);
        session.OnInput += (frame, data) => OnClientInput?.Invoke(id, frame, data);

        return id;
    }

    public MockServerSession()
    {
        OnClientDisconnect = id =>
        {
            Console.WriteLine($"Player with {id} disconnected.");
            idToSession_.Remove(id);
        };
    }

    public Task StartAsync()
    {
        Console.WriteLine("Creating server session.");
        return Task.CompletedTask;
    }

    Api? GetApi(long id)
    {
        lock (idToSession_)
        {
            idToSession_.TryGetValue(id, out var value);
            return value;
        }
    }

    public void TerminatePlayer(long playerId)
    {
        Api? api;

        lock (idToSession_)
        {
            api = GetApi(playerId);
            idToSession_.Remove(playerId);
        }

        api?.Terminate();

        Console.WriteLine($"Terminating player {playerId}.");
    }

    public void SignalMissedInput(long playerId, long frame, long currentFrame)
    {
        GetApi(playerId)?.MissedInput(frame, currentFrame);
        //Console.WriteLine($"Player {playerId} has failed to send input for frame {frame}.");
    }

    public void SignalEarlyInput(long playerId, long frame, long currentFrame)
    {
        GetApi(playerId)?.EarlyInput(frame, currentFrame);
        //Console.WriteLine($"Player {playerId} has sent unusualy early input for frame {frame} at frame {currentFrame}.");
    }

    public void InitiatePlayer(long playerId, long currentFrame, ReadOnlyMemory<byte> currentGameState)
    {
        GetApi(playerId)?.Initiate(currentFrame, currentGameState);
        //Console.WriteLine($"Initiating {playerId} with state at frame {currentFrame}.");
    }

    public void SendInputToPlayer(long playerId, long currentFrame, ReadOnlyMemory<byte> gameInput)
    {
        GetApi(playerId)?.GameInput(currentFrame, gameInput);
        //Console.WriteLine($"Sending game input to {playerId} for frame {currentFrame}.");
    }

    public event Action<long>? OnClientConnect;

    public event Action<long>? OnClientDisconnect;

    public event Action<long, long, ReadOnlyMemory<byte>>? OnClientInput;

    public void Dispose()
    {
        // TODO: do
    }
}
