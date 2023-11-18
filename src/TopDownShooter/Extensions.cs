using SFML.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopDownShooter.Extensions;

public static class Vector2fExtensions
{
    public static Vector2f Lerp(this Vector2f from, Vector2f to, float t) => from + (to - from) * t;
}
