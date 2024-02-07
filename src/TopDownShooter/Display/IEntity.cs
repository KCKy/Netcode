using SFML.Graphics;
using SFML.System;

namespace TopDownShooter.Display;

interface IEntity
{
   void DrawLerped(Displayer displayer, Vector2f origin, IEntity to, float t);
}
