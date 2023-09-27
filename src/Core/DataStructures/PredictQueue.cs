using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DataStructures;

public interface IPredictQueue<TElement> where TElement : struct
{
    void Reset(long nextFrame);
    void Enqueue(TElement element);
    long NextFrame { get; }
    TElement? Dequeue(long frame);
}

public sealed class PredictQueue
{
    
}
