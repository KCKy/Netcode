using MemoryPack;
using SFML.Graphics;
using SFML.System;

namespace TestGame;

[MemoryPackable]
[MemoryPackUnion(0, typeof(PlayerAvatar))]
partial interface ILevelObject
{
    void Draw(RenderTarget target, Vector2f position, float unit);
    void DrawAuth(RenderTarget target, Vector2f position, float unit);
}
