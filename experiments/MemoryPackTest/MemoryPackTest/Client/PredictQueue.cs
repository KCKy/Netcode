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

    public ReadOnlyMemory<byte> GetInput(long frame)
    {
        return mapping_[frame];
    }

    public bool CheckDequeue(ReadOnlyMemory<byte> authoritativeInput, long frame)
    {
        bool equal = mapping_.TryGetValue(frame, out var input) && authoritativeInput.Span.SequenceEqual(input.Span);

        mapping_.Remove(frame - 1);

        if (!equal)
            AddPredict(authoritativeInput, frame);

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

        return equal;
    }

    public void ReplaceTimeline(PredictQueue<TPlayerInput, TServerInput> other)
    {
        (mapping_, other.mapping_) = (other.mapping_, mapping_);
    }
}
