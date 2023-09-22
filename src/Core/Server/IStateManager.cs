namespace Core.Server;

internal interface IStateManager<TPlayerInput, TServerInput, TGameState, TUpdateInfo>
{
    UpdateOutput Update(UpdateInput<TPlayerInput, TServerInput> input, ref TUpdateInfo info);
    TGameState State { get; }
    long Frame { get; }
    long GetChecksum();
    Memory<byte> Serialize();
}
