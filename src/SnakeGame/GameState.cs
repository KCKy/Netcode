﻿using Core;
using MemoryPack;
using SFML.System;
using System.Linq;

namespace SnakeGame;

[MemoryPackable]
sealed partial class Player
{
    public Direction? Direction;
    public Vector2i? Position;
}

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public long Frame = -1;

    public const int LevelWidth = 30;
    public const int LevelHeight = 30;
    public static double DesiredTickRate => 3;

    public static Vector2i SpawnPoint = new(0, 0);

    [MemoryPackInclude]
    public Dictionary<long, Player> IdToPlayer = new();

    public Level level_ = new(LevelWidth, LevelHeight);

    bool TrySpawnPlayer(long id, Player player)
    {
        ref ILevelObject? spawn = ref level_[SpawnPoint];

        if (spawn is not null)
            return false;

        spawn = new PlayerAvatar()
        {
            Id = id
        };

        player.Position = SpawnPoint;

        return true;
    }

    public void UpdatePlayer(long id, Player player)
    {
        if (player.Position is not { } oldPos)
            throw new ArgumentException("Player has not spawned yet.", nameof(player));

        if (player.Direction is not { } direction)
            return;

        Vector2i movement = direction.MapToMovement();
        Vector2i newPos = oldPos + movement;

        if (!level_.InBounds(newPos))
            return;

        ref ILevelObject? place = ref level_[newPos];

        if (place is not null)
            return;

        ref ILevelObject? oldPlace = ref level_[oldPos];

        (place, oldPlace) = (oldPlace, null);
        player.Position = newPos;
    }

    int GetOccupation(int x, int y) =>
        level_.At(x, y) switch
        {
            null => 0,
            Cell cell => cell.State != Cell.CellState.Newborn ? 1 : 0,
            _ => 1
        };

    int CountNeighbors(int ox, int oy) =>
        (from x in Enumerable.Range(-1, 3)
            from y in Enumerable.Range(-1, 3)
            where (x != 0 || y != 0)
            select GetOccupation(ox + x, oy + y)).Sum();

    void UpdateGame()
    {
        int width = level_.Width;
        int height = level_.Height;

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            ref ILevelObject? obj = ref level_[x, y];

            switch (obj)
            {
                case null:
                {
                    if (CountNeighbors(x, y) == 3)
                        obj = new Cell();
                    break;
                }
                case Cell cell:
                {
                    int neighbors = CountNeighbors(x, y);
                    cell.State = neighbors switch
                    {
                        < 2 => Cell.CellState.Dying,
                        > 3 => Cell.CellState.Dying,
                        _ => Cell.CellState.Alive
                    };
                    break;
                }
            }
        }

        int size = level_.Size;

        for (int i = 0; i < size; i++)
        {
            ref ILevelObject? obj = ref level_[i];

            if (obj is not Cell cell)
                continue;

            if (cell.State == Cell.CellState.Dying)
                obj = null;
            cell.State = Cell.CellState.Alive;
        }
    }

    void CheckRespawn(ServerInput serverInput)
    {
        if (serverInput.CellRespawnEventSeed == 0)
            return;

        Random random = new(serverInput.CellRespawnEventSeed);

        int size = level_.Size;

        for (int i = 0; i < size; i++)
        {
            ref ILevelObject? obj = ref level_[i];

            if (obj is not Cell and not null)
                continue;

            obj = random.NextSingle() > 0.66 ? new Cell() { State = Cell.CellState.Alive } : null;
        }
    }

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        UpdateGame();

        foreach ((long id, ClientInput input, bool disconnected) in updateInputs.ClientInput.Span)
        {
            if (disconnected)
            {
                IdToPlayer.Remove(id);
                continue;
            }

            if (!IdToPlayer.TryGetValue(id, out Player? player))
            {
                player = new();
                IdToPlayer.Add(id, player);
            }

            player.Direction = input.Direction;

            if (player.Position is not null)
            {
                UpdatePlayer(id, player);
                continue;
            }
            
            if (!input.Start)
                continue;

            TrySpawnPlayer(id, player);
        }

        CheckRespawn(updateInputs.ServerInput);

        Frame++;

        return UpdateOutput.Empty;
    }
}
