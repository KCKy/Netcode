using System.Collections.Generic;
namespace FrameworkTest;

public sealed class ClientInputs<TClientInput>
{
    readonly Dictionary<long, TClientInput> mapping_ = new();

    public bool TryGet(long frame, out TClientInput input)
    {
        return mapping_.TryGetValue(frame, out input!);
    }
    
    public void AddNext(TClientInput input, long frame)
    {
        mapping_.Add(frame, input);
    }

    public void ClearFrame(long frame)
    {
        mapping_.Remove(frame);
    }
}
