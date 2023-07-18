using MessagePack;

public class GameStateAttribute : Attribute { }

[MessagePackObject]
public class MyState
{
    public int Value;
}

static class GeneratorTest
{
    static void Main()
    {
        var x = new MyState();

        byte[] serialized = MessagePackSerializer.Serialize(x);
        Console.WriteLine(BitConverter.ToString(serialized));

        var y = MessagePackSerializer.Deserialize<MyState>(serialized);
        Console.WriteLine(y.Value);
    }
}
