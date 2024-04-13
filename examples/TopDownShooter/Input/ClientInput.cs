using Kcky.GameNewt.Providers;
using MemoryPack;

namespace TopDownShooter.Input;

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

class ClientInputPredictor : IClientInputPredictor<ClientInput>
{
    public void PredictInput(ref ClientInput previous)
    {
        previous.Shoot = false;
        previous.ShootX = 0;
        previous.ShootY = 0;
        previous.ShootFrameOffset = 0;
        previous.Respawn = false;
    }
}
