using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Core.Transport;

namespace DefaultTransport.TcpTransport;

public class TcpServerTransport<TIn, TOut> : IServerTransport<TIn, TOut>
{
    readonly TcpListener listener_;

    public IPEndPoint Local { get; }

    public TcpServerTransport(IPEndPoint local)
    {
        Local = local;
        listener_ = new TcpListener(local);
    }

    public event Action<long, TIn>? OnMessage;
    public event Action<long>? OnClientJoin;
    public event Action<long, ClientFinishReason>? OnClientFinish;

    async Task ManageClientsAsync()
    {
        while (true)
        {
            TcpClient client;
            NetworkStream stream;

            try
            {
                client = await listener_.AcceptTcpClientAsync();
            }
            catch (SocketException)
            {
                continue;
            }

            try
            {
                stream = client.GetStream();
            }
            catch (InvalidOperationException)
            {
                client.Dispose();
                continue;
            }


        }
    }

    public async Task Run()
    {
        try
        {
            listener_.Start();
        }
        catch (SocketException)
        {
            throw;
            // TODO: do
        }

        Task manageTask = ManageClientsAsync();
    }

    public void SendReliable(TOut message)
    {
        throw new NotImplementedException();
    }

    public void SendReliable(TOut message, long id)
    {
        throw new NotImplementedException();
    }

    public void SendUnreliable(TOut message)
    {
        throw new NotImplementedException();
    }

    public void SendUnreliable(TOut message, long id)
    {
        throw new NotImplementedException();
    }

    public void Terminate(long id)
    {
        throw new NotImplementedException();
    }
}
