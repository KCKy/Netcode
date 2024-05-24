using SFML.Graphics;
using SFML.System;

namespace TopDownShooter;

interface IEntity
{
   void DrawLerped(RenderTarget renderTarget, Vector2f origin, IEntity to, float t, GameClient gameClient);
   bool IsPredicted(int localId);
}
