using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SFML.System;

namespace SimpleGame;
public static class SfmlExtensions
{
    public static void Deconstruct(this Vector2i vector, out int x, out int y)
    {
        x = vector.X;
        y = vector.Y;
    }
}
