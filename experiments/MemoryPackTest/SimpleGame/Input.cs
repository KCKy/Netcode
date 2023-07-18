using MemoryPack;

[MemoryPackable]
public partial record struct PlayerInput (bool Up, bool Down, bool Left, bool Right);

[MemoryPackable]
public partial record struct ServerInput { }
