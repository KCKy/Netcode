using System;
using System.Collections;
using MemoryPack;
using System.Collections.Generic;
using System.Linq;
using FrameworkTest;
using SFML.System;

namespace SimpleGame;

[MemoryPackable]
[MemoryPackUnion(Wall.UnionId, typeof(Wall))]
[MemoryPackUnion(Stone.UnionId, typeof(Stone))]
[MemoryPackUnion(Player.UnionId, typeof(Player))]
public partial interface ISceneObject
{
    public bool Solid { get; }
    public bool Destructible { get; }
    public int Id { get; }
}

[MemoryPackable]
public partial class Wall : ISceneObject
{
    public const int UnionId = 0;
    public bool Solid => true;
    public bool Destructible => true;
    public int Id => UnionId;
}

[MemoryPackable]
public partial class Stone : ISceneObject
{
    public const int UnionId = 1;
    public bool Solid => true;
    public bool Destructible => false;
    public int Id => UnionId;
}

[MemoryPackable]
public partial class Player : ISceneObject
{
    public const int UnionId = 2;
    public bool Solid => true;
    public bool Destructible => false;
    public int Id => UnionId;
}

// TODO: analyzer which checks a type is in the union

[MemoryPackable]
public partial struct Chunk : IEnumerable<(Vector2i Pos, ISceneObject Obj)>
{
    public const int ChunkSize = 32;

    public ISceneObject?[] Objects = new ISceneObject?[ChunkSize * ChunkSize];

    public Chunk() { }

    public ISceneObject? this[int x, int y]
    {
        get => Objects[x + y * ChunkSize];
        set => Objects[x + y * ChunkSize] = value;
    }

    public long Count => (from o in Objects where o is not null select o).LongCount();

    public IEnumerator<(Vector2i Pos, ISceneObject Obj)> GetEnumerator()
    {
        for (int i = 0; i < Objects.Length; i++)
        {
            if (Objects[i] is ISceneObject obj)
            {
                (int y, int x) = Math.DivRem(i, ChunkSize);
                yield return (new(x, y), obj);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class GameMath
{
    /// <summary>
    /// Improved modulo, returns values in range [0, divisor - 1]
    /// </summary>
    /// <param name="x">Value which is divided</param>
    /// <param name="divisor">The divisor</param>
    /// <returns>The mod of x / divisor </returns>
    public static int Mod(int x, int divisor) => (divisor + x % divisor) % divisor;

    /// <summary>
    /// Alternative whole division which alway rounds down to the nearest integer
    /// </summary>
    /// <param name="x">Value which is divided</param>
    /// <param name="divisor">The divisor</param>
    /// <returns>x / divisor rounded down to the nearest integer</returns>
    public static int Div(int x, int divisor) => x / divisor - Convert.ToInt32(((x < 0) ^ (divisor < 0)) && (x % divisor != 0));
}

[MemoryPackable]
public partial struct Layer : IEnumerable<(Vector2i Pos, ISceneObject Obj)>
{
    public Dictionary<(int, int), Chunk> Chunks = new();

    public Layer() { }

    Chunk GetChunk(int x, int y, out int tileX, out int tileY)
    {
        int chunkX = GameMath.Div(x, Chunk.ChunkSize);
        int chunkY = GameMath.Div(y, Chunk.ChunkSize);

        tileX = GameMath.Mod(x, Chunk.ChunkSize);
        tileY = GameMath.Mod(y, Chunk.ChunkSize);

        (int, int) pos = (chunkX, chunkY);

        if (!Chunks.TryGetValue(pos, out Chunk value))
        {
            value = new Chunk();
            Chunks.Add(pos, value);
        }

        return value;
    }

    public long Count => (from c in Chunks.Values select c.Count).Sum();

    public ISceneObject? this[Vector2i pos]
    {
        get => this[pos.X, pos.Y];
        set => this[pos.X, pos.Y] = value;
    }

    public ISceneObject? this[int x, int y]
    {
        get => GetChunk(x, y, out int tileX, out int tileY)[tileX, tileY];
        set
        {
            Chunk chunk = GetChunk(x, y, out int tileX, out int tileY);
            chunk[tileX, tileY] = value;
        }
    }

    public IEnumerator<(Vector2i Pos, ISceneObject Obj)> GetEnumerator()
    {
        foreach (((int cX, int cY), Chunk chunk) in Chunks)
        {
            var cPos = new Vector2i(cX, cY) * Chunk.ChunkSize;

            foreach ((Vector2i pos, ISceneObject obj) in chunk)
                yield return (cPos + pos, obj);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[MemoryPackable]
public partial class GameState : IGameState<PlayerInput, ServerInput>
{
    public long Tick = -1;

    public Dictionary<long, Vector2i> Players = new();

    public Layer Objects = new();

    public (long Id, PlayerInput Input, bool terminated)[] Inputs = Array.Empty<(long, PlayerInput, bool)>();

    const int StoneArea = 50;

    static readonly Vector2i Spawn = new(0, 0);

    public GameState()
    {
        // This needs to be deterministic

        Random random = new(42); 

        for (int x = -StoneArea; x <= StoneArea; x++)
        for (int y = -StoneArea; y <= StoneArea; y++)
        {
            Objects[x, y] = random.Pick<ISceneObject>(new Stone(), new Stone(), new Wall());
        }
    }

    public UpdateOutput Update(in Input<PlayerInput, ServerInput> inputs)
    {
        /*
        Stopwatch watch = new();

        watch.Start();
        */

        Tick++;

        // TODO: it depents on the order of inputs, which is fine?

        Inputs = inputs.PlayerInputs; // TODO: make sure this does not break anything

        foreach ((long id, PlayerInput input, bool terminated) in inputs.PlayerInputs)
        {
            if (terminated)
            {
                Players.Remove(id);
                continue;
            }

            // INPUT

            int dx = (input.Right ? 1 : 0) + (input.Left ? -1 : 0);
            int dy = (input.Down ? 1 : 0) + (input.Up ? -1 : 0);

            Vector2i d = new(dx, dy);

            // SPAWN

            if (!Players.TryGetValue(id, out var pos))
            {
                if (Objects[Spawn] is Player)
                    continue;

                Player player = new();
                Objects[Spawn] = player;
                Players.Add(id, Spawn);
                continue;
            }

            // MOVE

            switch (Objects[pos + d])
            {
                case Player:
                    continue;
            }

            Players[id] = pos + d;
            (Objects[pos], Objects[pos + d]) = (null, Objects[pos]);
        }

        /*
        watch.Stop();

        Console.WriteLine($"Finished update in {watch.ElapsedMilliseconds} ms ({watch.ElapsedTicks})");
        */

        return new();
    }

    public static float DesiredTickRate => 20f;
}

public static class RandomExtensions
{
    public static T Pick<T>(this Random random, params T[] entities)
    {
        int i = random.Next(entities.Length);
        return entities[i];
    }
}