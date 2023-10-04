using MemoryPack;
using SFML.Graphics;
using SFML.System;

namespace TestGame;

[MemoryPackable]
[MemoryPackUnion(0, typeof(Food))]
[MemoryPackUnion(1, typeof(PlayerAvatar))]
partial interface ILevelObject
{
    void Draw(RenderTarget target, Vector2f position, float unit);
    void DrawAuth(RenderTarget target, Vector2f position, float unit);
}

enum FoodType : byte
{
    Apple,
    Carrot
}

[MemoryPackable]
sealed partial class Food : ILevelObject
{
    [MemoryPackInclude]
    public FoodType FoodType { get; set; } = FoodType.Apple;

    public void Draw(RenderTarget target, Vector2f position, float unit)
    {
        
    }

    public void DrawAuth(RenderTarget target, Vector2f position, float unit)
    {
        
    }
}
