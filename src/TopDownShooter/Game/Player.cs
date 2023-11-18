using MemoryPack;
using Serilog;
using Serilog.Core;
using SFML.Graphics;
using SFML.System;
using TopDownShooter.Display;
using TopDownShooter.Extensions;
using TopDownShooter.Input;
using Useful;

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
partial class Player : IEntity
{
    [MemoryPackInclude]
    long id_;

    [MemoryPackInclude]
    Position position_ = new();

    [MemoryPackInclude]
    Vec2<Fixed> velocity_;

    [MemoryPackInclude]
    long entityId_;

    [MemoryPackConstructor]
    Player() { }
    
    public override string ToString()
    {
        return Position.ToString();
    }

    public Player(long entityId)
    {
        entityId_ = entityId;
    }

    static readonly Fixed MovementSpeed = 10;
    static readonly Fixed ColliderRadiusSquared = 32 * 32;

    static readonly Fixed Friction = new(1L << 31);

    static readonly ILogger logger = Log.ForContext<Player>();

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
        var relativePos = targetPosition - origin;
        var projection = relativePos.Project(ray);
        var delta = relativePos - projection;

        return delta.Dot(delta) <= ColliderRadiusSquared
               && projection.X > 0 ? relativePos.X > 0 : relativePos.X <= 0;
    }

    static readonly ILogger logger_ = Log.ForContext<Player>();

    public ref Vec2<Fixed> Position => ref position_.Current;

    public void DrawSelf(Renderer renderer, IEntity to, float t)
    {
        if (to as Player is not { EntityId: var otherID } target || otherID != entityId_)
        {
            logger_.Error("Invalid lerp target for player.");
            return;
        }
            
        ref var pos = ref Position;
        ref var tpos = ref target.Position;

        Vector2f fromPos = new(pos.X, pos.Y);
        Vector2f toPos = new(tpos.X, tpos.Y);
        Vector2f lerpedPos = fromPos.Lerp(toPos, t);

        renderer.DrawPlayer(entityId_, lerpedPos, Color.Blue);
    }

    public long EntityId => entityId_;
}
