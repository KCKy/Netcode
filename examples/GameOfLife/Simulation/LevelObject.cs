using MemoryPack;
using SFML.Graphics;
using SFML.System;

namespace GameOfLife;

[MemoryPackable]
[MemoryPackUnion(0, typeof(PlayerAvatar))]
[MemoryPackUnion(1, typeof(Cell))]
partial interface ILevelObject
{
    void Draw(RenderTarget target, Vector2f position, float unit);
}
