namespace Basic;

static class Program
{
    static void Main()
    {
        Console.WriteLine("Run client or server [c/s]? ");
        char? command = Console.ReadLine()?.ToLower().FirstOrDefault();

        switch (command)
        {
            case 's':
                GameServer server = new();
                server.RunAsync().Wait();
                return;
            default:
                GameClient client = new();
                client.Run();
                return;
        }
    }
}
