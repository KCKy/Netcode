using MemoryPack;

namespace TopDownShooter.Input;

[MemoryPackable]
partial class ClientInput
{
    public sbyte Horizontal;
    public sbyte Vertical;
    public bool Start;
}
