using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Server;

public enum ClientRemoveReason
{
    Disconnect,
    Kicked,
    TimedOut
}

public interface IServerSession
{
    void AddClient(long id);
    void AddInput(long id, long frame, Memory<byte> input);
    void RemoveClient(long id, ClientRemoveReason reason);
}
