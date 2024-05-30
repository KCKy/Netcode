using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.PredictionSimple;

[MemoryPackable]
partial class ServerInput;

[MemoryPackable]
partial class ClientInput
{
    public int MyNumber = -1;
}

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public long Frame = -1;
    public int MyNumber;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        Frame++;

        Span<UpdateClientInfo<ClientInput>> inputs = updateInputs.ClientInputInfos.Span;
        if (inputs.IsEmpty)
            return UpdateOutput.Empty;

        UpdateClientInfo<ClientInput> input = inputs[0];
        if (input.Terminated)
            return UpdateOutput.Terminate;

        if (inputs[0].Input.MyNumber > 0)
            MyNumber = inputs[0].Input.MyNumber;

        return UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
