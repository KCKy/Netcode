namespace TopDownShooter.Display;

interface IEntity
{
   void DrawSelf(Renderer displayer, IEntity to, float t);
}
