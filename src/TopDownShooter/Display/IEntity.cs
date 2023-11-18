using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopDownShooter.Display
{
    interface IEntity
    {
       void DrawSelf(Renderer displayer, IEntity to, float t);
    }
}
