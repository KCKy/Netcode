using MemoryPack;

namespace TopDownShooter;

[MemoryPackable]
partial class ClientInput
{
    public sbyte Horizontal;
    public sbyte Vertical;
    public bool Start;
    public bool Shoot;
    public int ShootX;
    public int ShootY;
    public int ShootFrameOffset;
    public bool Respawn;
}

[MemoryPackable]
partial class ServerInput { }

static class ClientInputPrediction
{
    public static void PredictClientInput(ref ClientInput previous)
    {
        previous.Shoot = false;
        previous.ShootX = 0;
        previous.ShootY = 0;
        previous.ShootFrameOffset = 0;
        previous.Respawn = false;
    }
}
