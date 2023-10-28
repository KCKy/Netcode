using Core;
using MemoryPack;
using SFML.System;

namespace TestGame;

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
    public static double DesiredTickRate => 20;

    public static Vector2i SpawnPoint = new(0, 0);

    [MemoryPackInclude]
    public Dictionary<long, Player> IdToPlayer = new();

    public Level level_ = new(LevelWidth, LevelHeight);

    void HandleFoodEvent(in FoodSpawnEvent foodEvent)
    {
        ref ILevelObject? place = ref level_[foodEvent.X, foodEvent.Y];

        if (place is not null)
            return;
        
        Food food = new()
        {
            FoodType = foodEvent.Type
        };

        place = food;
    }

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

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
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

        Frame++;

        if (updateInputs.ServerInput.FoodSpawnEvent is { } foodEvent)
            HandleFoodEvent(foodEvent);

        return UpdateOutput.Empty;
    }
}
