using Core;
using MemoryPack;

namespace CoreTests;

[MemoryPackable]
partial class MockInput
{
    public int A;
    public byte B;
    public long C;
    public float D;

    [MemoryPackConstructor]
    public MockInput() { }

    public MockInput(int a, byte b, long c, float d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }
}

static class MockInputExtensions
{
    public static void Assert(this in UpdateClientInfo<MockInput> info, long id, MockInput input, bool terminated)
    {
        Xunit.Assert.Equal(id, info.Id);
        Xunit.Assert.True(MemoryPackSerializer.Serialize(input).SequenceEqual(MemoryPackSerializer.Serialize(info.Input)));
        Xunit.Assert.Equal(terminated, info.Terminated);
    }
}
