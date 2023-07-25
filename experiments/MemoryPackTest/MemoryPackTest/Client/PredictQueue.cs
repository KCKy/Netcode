using System;
using System.Collections.Generic;
using MemoryPack;

namespace FrameworkTest;

public sealed class PredictQueue<TPlayerInput, TServerInput>
{
    Dictionary<long, ReadOnlyMemory<byte>> mapping_ = new();

    public void AddPredict(ReadOnlyMemory<byte> input, long frame)
    {
        if (!mapping_.TryAdd(frame, input))
            mapping_[frame] = input;
    }

    public bool CheckDequeue(ReadOnlyMemory<byte> authoritativeInput, long frame)
    { 

        if (!mapping_.TryGetValue(frame, out var input))
            return false;
        /*
        var x = MemoryPackSerializer.Deserialize<Input<TPlayerInput, TServerInput>>(authoritativeInput.Span);
        
        
        Console.WriteLine($"<->");

        foreach (var (a, b, c) in x.PlayerInputs)
            Console.WriteLine($"{a} {b} {c}");

        Console.WriteLine($"-");
        
        var y = MemoryPackSerializer.Deserialize<Input<TPlayerInput, TServerInput>>(input.Span);

        foreach (var (a, b, c) in y.PlayerInputs)
            Console.WriteLine($"{a} {b} {c}");

        Console.WriteLine($"<->");
        */
        return  authoritativeInput.Span.SequenceEqual(input.Span);
    }

    public void ReplaceTimeline(PredictQueue<TPlayerInput, TServerInput> other)
    {
        (mapping_, other.mapping_) = (other.mapping_, mapping_);
    }
}
