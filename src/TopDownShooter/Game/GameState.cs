using Core;
using Core.Utility;
using MemoryPack;
using Serilog;
using Serilog.Core;
using TopDownShooter.Display;
using TopDownShooter.Input;
using Useful;

namespace TopDownShooter.Game;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    [MemoryPackInclude] Dictionary<long, ClientInfo> idToClient_ = new();
    [MemoryPackInclude] long lastConnected_ = long.MinValue;
    [MemoryPackInclude] long entityCounter_ = 0;

    public IEnumerable<(long Id, IEntity Entity)> GetEntities(PooledBufferWriter<byte> copyWriter)
    {
        foreach (ClientInfo info in idToClient_.Values)
        {
            if (info.Avatar is not { } avatar)
                continue;
            
            Player copy = new(avatar.EntityId);
            copy.Position = avatar.Position;
            yield return (copy.EntityId, copy);
        }
    }

    void UpdatePlayer(UpdateClientInfo<ClientInput> info)
    {
        (long id, ClientInput input, bool disconnected) = info;

        if (!idToClient_.TryGetValue(id, out ClientInfo? client))
        {
            if (id <= lastConnected_)
                return;

            lastConnected_ = id;
            client = new();
            idToClient_.Add(id, client);
        }

        if (disconnected)
            idToClient_.Remove(id);

        if (client.Avatar is not { } avatar)
        {
            logger.Information("Created player avatar for: {Id}", id);
            avatar = new(entityCounter_++);
            client.Avatar = avatar;
        }

        avatar.Update(input);
    }

    static readonly ILogger logger = Log.ForContext<GameState>();

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        foreach (var info in updateInputs.ClientInput.Span)
            UpdatePlayer(info);

        return UpdateOutput.Empty;
    }

    public static double DesiredTickRate => 20;
}

[MemoryPackable]
partial class ClientInfo
{
    public Player? Avatar = null;
}
