using System;
using System.Linq;
using System.Net;
using Kcky.Useful;
using Microsoft.Extensions.Logging;

namespace TopDownShooter;

class Program
{
    static readonly int DefaultPort = 45963;
    static readonly IPEndPoint DefaultTarget = new(IPAddress.Loopback, DefaultPort);

    static void Main()
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Trace));

        Console.WriteLine("Run client or server [c/s]? ");
        char? command = Console.ReadLine()?.ToLower().FirstOrDefault();
        
        switch (command)
        {
            case 's':
                int port = Command.GetPort("Enter server port: ", DefaultPort);
                GameServer server = new(port, loggerFactory);
                server.RunAsync().Wait();
                return;
            default:
                IPEndPoint target = Command.GetEndPoint("Enter an address to connect to: ", DefaultTarget);
                GameClient client = new(target, loggerFactory);
                client.Run();
                return;
        }
    }
}
