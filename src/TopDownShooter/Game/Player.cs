using System;
using System.Collections.Generic;
using System.Reflection;
using MemoryPack;
using Serilog;
using Serilog.Core;
using SFML.Graphics;
using SFML.System;
using TopDownShooter.Display;
using TopDownShooter.Input;
using Useful;
using SfmlExtensions;

namespace TopDownShooter.Game;

[MemoryPackable]
partial struct Position
{
    const int HistorySize = 10;

    [MemoryPackInclude]
    Vec2<Fixed>[] history_ = new Vec2<Fixed>[HistorySize];

    [MemoryPackInclude]
    int current_ = 0;

    public ref Vec2<Fixed> Current => ref history_[current_];

    public void Update(Vec2<Fixed> value)
    {
        current_ = (current_ + 1) % HistorySize;
        history_[current_] = value;
    }

    public readonly Vec2<Fixed>? GetHistory(int index)
    {
        if (index is > 0 or <= -HistorySize)
            return null;

        return history_[(current_ + index + HistorySize) % HistorySize];
    }

    public Position() { }
}

[MemoryPackable]
sealed partial class Player : IEntity
{
    [MemoryPackInclude]
    long id_;

    [MemoryPackInclude]
    Position position_ = new();

    [MemoryPackInclude]
    Vec2<Fixed> velocity_;

    [MemoryPackInclude]
    long entityId_;

    [MemoryPackInclude]
    long playerId_;

    [MemoryPackConstructor]
    Player() { }
    
    public override string ToString()
    {
        return Position.ToString();
    }

    public Player(long entityId, long playerId)
    {
        entityId_ = entityId;
        playerId_ = playerId;
    }

    static readonly Fixed MovementSpeed = 10;
    static readonly Fixed ColliderRadiusSquared = 32 * 32;
    static readonly Fixed Friction = ((Fixed)2).Reciprocal;

    public void Update(ClientInput input)
    {
        int horizontal = Math.Sign(input.Horizontal);
        int vertical = Math.Sign(input.Vertical);

        if (horizontal != 0)
            velocity_.X = horizontal * MovementSpeed;
        if (vertical != 0)
            velocity_.Y = vertical * MovementSpeed;

        position_.Update(Position + velocity_);
        velocity_ *= Friction;
    }

    public static bool CheckHit(Vec2<Fixed> origin, Vec2<Fixed> ray, Vec2<Fixed> targetPosition)
    {
        var diff = origin + ray - targetPosition;
        return diff.Dot(diff) <= ColliderRadiusSquared;
    }

    public ref Vec2<Fixed> Position => ref position_.Current;

    public Vec2<Fixed>? GetPositionHistory(int index) => position_.GetHistory(index);

    public void Shoot(Vec2<Fixed> direction, List<Player> avatars, int histIndex)
    {
        Log.Information("Shooting with hist index: {HistIndex}.", histIndex);

        if (position_.GetHistory(histIndex) is not { } position)
            return;

        foreach (Player player in avatars)
        {
            if (player.GetPositionHistory(histIndex) is not { } targetPos)
                continue;

            if (!CheckHit(position, direction, targetPos))
                continue;
            
            player.Position = new(0, 0);
            player.velocity_ = new(0, 0);
        }
    }

    const int Radius = 32;
    static readonly CircleShape PlayerShape = new()
    {
        Origin = new(Radius, Radius),
        Radius = Radius
    };

    static readonly Palette PlayerPalette = new()
    {
        A = new(.5f, .5f, .5f),
        B = new(.5f, .5f, .5f),
        C = new(1, 1, 1),
        D = new(.8f, .9f, .3f)
    };

    const float PlayerPaletteSpacing = 0.3f;

    void Draw(RenderTarget renderTarget, Vector2f position)
    {
        PlayerShape.Position = position;
        PlayerShape.FillColor = PlayerPalette[playerId_ * PlayerPaletteSpacing].ToColor();
        renderTarget.Draw(PlayerShape);
    }

    public void DrawLerped(Displayer displayer, Vector2f origin, IEntity to, float t)
    {
        if (to as Player is not { EntityId: var otherId } target || otherId != entityId_)
            throw new ArgumentException("Invalid target entity.", nameof(to));

        ref var rawSourcePosition = ref Position;
        ref var rawTargetPosition = ref target.Position;

        Vector2f sourcePosition = new(rawSourcePosition.X, rawSourcePosition.Y);
        Vector2f targetPosition = new(rawTargetPosition.X, rawTargetPosition.Y);
        Vector2f position = sourcePosition.Lerp(targetPosition, t);

        if (displayer.Id == playerId_)
            displayer.Center = position;

        Draw(displayer.Window, position - origin);
    }

    public bool IsPredicted(long localId) => playerId_ == localId;

    public long EntityId => entityId_;
}
