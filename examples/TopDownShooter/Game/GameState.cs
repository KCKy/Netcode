using System.Collections.Generic;
using Kcky.GameNewt;
using MemoryPack;
using Serilog;
using TopDownShooter.Display;
using TopDownShooter.Input;
using Kcky.Useful;

namespace TopDownShooter.Game;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    [MemoryPackInclude] SortedDictionary<int, ClientInfo> idToClientInfo_ = new();
    [MemoryPackInclude] int lastConnected_ = int.MinValue;
    [MemoryPackInclude] int entityCounter_ = 0;

    public IEnumerable<(int Id, IEntity Entity)> GetEntities(PooledBufferWriter<byte> copyWriter)
    {
        foreach ((int id, ClientInfo info) in idToClientInfo_)
        {
            if (info.Avatar is not { } avatar)
                continue;
            
            PlayerAvatar copy = new(avatar.EntityId, id)
            {
                Position = avatar.Position
            };
            yield return (copy.EntityId, copy);
        }
    }

    PlayerAvatar? UpdatePlayerAvatar(UpdateClientInfo<ClientInput> info)
    {
        (int id, ClientInput input, bool disconnected) = info;
        
        if (!idToClientInfo_.TryGetValue(id, out ClientInfo? client))
        {
            if (id <= lastConnected_)
                return null;

            lastConnected_ = id;
            client = new();
            idToClientInfo_.Add(id, client);
        }

        if (disconnected)
            idToClientInfo_.Remove(id);

        if (client.Avatar is not { } avatar)
        {
            Log.Information("Created player avatar for: {Id}", id);
            avatar = new(entityCounter_++, id);
            client.Avatar = avatar;
        }

        avatar.Update(input);
        return avatar;
    }

    PlayerAvatar? GetAvatar(UpdateClientInfo<ClientInput> info)
    {
        if (info.Terminated)
            return null;

        return idToClientInfo_.TryGetValue(info.Id, out ClientInfo? client) ? client.Avatar : null;
    }

    void ProcessShooting(UpdateClientInfo<ClientInput> info, List<PlayerAvatar> avatars)
    {
        ClientInput input = info.Input;

        if (!input.Shoot)
            return;

        PlayerAvatar? avatar = GetAvatar(info);
        avatar?.Shoot(new(input.ShootX, input.ShootY), avatars, -input.ShootFrameOffset);
    }

    void ProcessRespawn(UpdateClientInfo<ClientInput> info)
    {
        ClientInput input = info.Input;

        if (!input.Start)
            return;

        PlayerAvatar? avatar = GetAvatar(info);
        avatar?.Respawn();
    }

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        List<PlayerAvatar> avatars = new();
        var clientInputs = updateInputs.ClientInputInfos.Span;

        foreach (var info in clientInputs)
        {
            PlayerAvatar? avatar = UpdatePlayerAvatar(info);
            if (avatar is not null)
                avatars.Add(avatar);
        }
            
        foreach (var info in clientInputs)
            ProcessRespawn(info);

        foreach (var info in clientInputs)
            ProcessShooting(info, avatars);

        return UpdateOutput.Empty;
    }

    public static double DesiredTickRate => 20;
}

[MemoryPackable]
partial class ClientInfo
{
    public PlayerAvatar? Avatar = null;
}
