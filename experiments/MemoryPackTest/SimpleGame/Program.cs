using SFML.Graphics;
using SFML.Window;

static class Program
{
    static void Main()
    {
        VideoMode mode = new VideoMode(1280, 720);
        RenderWindow window = new RenderWindow(mode, "Framework Test");
        window.SetVerticalSyncEnabled(true);

        window.Closed += (sender, args) => window.Close();

        while (window.IsOpen)
        {
            window.DispatchEvents();
            window.Clear(Color.Black);
            window.Display();
        }
    }
}
