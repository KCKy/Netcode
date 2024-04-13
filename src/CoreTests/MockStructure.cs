using System.Linq;
using MemoryPack;

namespace Kcky.GameNewt.Tests;

[MemoryPackable]
sealed partial class MockStructure
{
    public int A;
    public byte B;
    public long C;
    public float D;

    [MemoryPackConstructor]
    public MockStructure() { }

    public MockStructure(int a, byte b, long c, float d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }
}

static class MockStructureExtensions
{
    public static void Assert(this in UpdateClientInfo<MockStructure> info, long id, MockStructure structure, bool terminated)
    {
        Xunit.Assert.Equal(id, info.Id);
        Xunit.Assert.True(MemoryPackSerializer.Serialize(structure).SequenceEqual(MemoryPackSerializer.Serialize(info.Input)));
        Xunit.Assert.Equal(terminated, info.Terminated);
    }
}
