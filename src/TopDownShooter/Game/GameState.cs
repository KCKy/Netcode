using System.Collections.Generic;
using Core;
using MemoryPack;
using Serilog;
using TopDownShooter.Display;
using TopDownShooter.Input;
using Useful;
using static SFML.Graphics.Font;

namespace TopDownShooter.Game;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    [MemoryPackInclude] SortedDictionary<long, ClientInfo> idToClient_ = new();
    [MemoryPackInclude] long lastConnected_ = long.MinValue;
    [MemoryPackInclude] long entityCounter_ = 0;

    public IEnumerable<(long Id, IEntity Entity)> GetEntities(PooledBufferWriter<byte> copyWriter)
    {
        foreach ((long id, ClientInfo info) in idToClient_)
        {
            if (info.Avatar is not { } avatar)
                continue;
            
            Player copy = new(avatar.EntityId, id)
            {
                Position = avatar.Position
            };
            yield return (copy.EntityId, copy);
        }
    }

    void UpdatePlayers(UpdateClientInfo<ClientInput> info, List<Player> avatars)
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
            Log.Information("Created player avatar for: {Id}", id);
            avatar = new(entityCounter_++, id);
            client.Avatar = avatar;
        }

        avatar.Update(input);
        avatars.Add(avatar);
    }

    Player? GetAvatar(UpdateClientInfo<ClientInput> info)
    {
        if (info.Terminated)
            return null;

        return idToClient_.TryGetValue(info.Id, out ClientInfo? client) ? client.Avatar : null;
    }

    void ProcessShooting(UpdateClientInfo<ClientInput> info, List<Player> avatars)
    {
        ClientInput input = info.Input;

        if (!input.Shoot)
            return;

        Player? avatar = GetAvatar(info);
        avatar?.Shoot(new(input.ShootX, input.ShootY), avatars, -input.ShootFrameOffset);
    }

    void ProcessRespawn(UpdateClientInfo<ClientInput> info)
    {
        ClientInput input = info.Input;

        if (!input.Start)
            return;

        Player? avatar = GetAvatar(info);
        avatar?.Respawn();
    }

    readonly List<Player> tempAvatarList_ = new(); // Not part of state

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        tempAvatarList_.Clear();

        foreach (var info in updateInputs.ClientInput.Span)
            UpdatePlayers(info, tempAvatarList_);

        foreach (var info in updateInputs.ClientInput.Span)
            ProcessRespawn(info);

        foreach (var info in updateInputs.ClientInput.Span)
            ProcessShooting(info, tempAvatarList_);

        return UpdateOutput.Empty;
    }

    public static double DesiredTickRate => 20;
}

[MemoryPackable]
partial class ClientInfo
{
    public Player? Avatar = null;
}
