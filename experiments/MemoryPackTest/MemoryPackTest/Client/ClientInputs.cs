using System.Collections.Generic;
using System.Diagnostics;

namespace FrameworkTest;

public sealed class ClientInputs<TClientInput>
{
    readonly Dictionary<long, TClientInput> mapping_ = new();

    public bool TryGet(long frame, out TClientInput input)
    {
        Debug.Assert(mapping_.Count < 256);
        return mapping_.TryGetValue(frame, out input!);
    }
    
    public void AddNext(TClientInput input, long frame)
    {
        Debug.Assert(mapping_.Count < 256);
        mapping_.Add(frame, input);
    }

    public void ClearFrame(long frame)
    {
        Debug.Assert(mapping_.Count < 256);
        mapping_.Remove(frame);
    }
}
