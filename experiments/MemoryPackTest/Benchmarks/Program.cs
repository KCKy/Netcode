using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FrameworkTest;
using MemoryPack;
using SFML.System;
using SimpleGame;
using System.Collections.Generic;

static class Program
{
    static void Main()
    {
        var summary = BenchmarkRunner.Run<MemoryPackBenchmark>();
        Console.WriteLine("COMPLETE");
        Console.WriteLine(summary);
        Console.WriteLine(summary.ResultsDirectoryPath);
    }
}

[MemoryPackable]
public partial class DictionaryHolderUnion
{
    public Dictionary<long, ISceneObject> Objects = new();
}

[MemoryPackable]
public partial class DictionaryHolderId
{
    public Dictionary<long, int> Objects = new();

}

[MemoryPackable]
public partial class DictionaryHolderUnionVector
{
    public Dictionary<Vector2i, ISceneObject> Objects = new();
}

[MemoryPackable]
public partial class DictionaryHolderIdVector
{
    public Dictionary<Vector2i, int> Objects = new();
}


public class MemoryPackBenchmark
{
    (long Id, ISceneObject Object)[] Data = Array.Empty<(long, ISceneObject)>();

    const int ObjectCount = 10000;

    DictionaryHolderUnion unionDictionary_ = new();
    DictionaryHolderId idDictionary_ = new();
    DictionaryHolderUnionVector vectorUnionDictionary_ = new();
    DictionaryHolderIdVector vectorIdDictionary_ = new();

    GameState gameState_ = new();

    byte[] unionDictionaryData_ = Array.Empty<byte>();
    byte[] idDictionaryData_ = Array.Empty<byte>();
    byte[] vectorUnionDictionaryData_ = Array.Empty<byte>();
    byte[] vectorIdDictionaryData_ = Array.Empty<byte>();

    byte[] gameStateData_ = Array.Empty<byte>();

    public static GameState MockUpdate(GameState state)
    {
        Input<PlayerInput, ServerInput> input = new(new(), new[]
        {
            (10L, new PlayerInput(true, false, false, false), false),
            (20L, new PlayerInput(false, false, false, false), false),
            (30L, new PlayerInput(false, true, false, false), false),
            (40L, new PlayerInput(false, false, true, false), false),
            (50L, new PlayerInput(false, true, false, true), false)
        });

        for (int i = 0; i < 40; i++)
            state.Update(input);
        return state;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        Random random = new(42);
        
        var choices = new List<ISceneObject>()
        {
            new Wall(),
            new Stone(),
            new Player()
        }.ToArray();

        gameState_ = MockUpdate(new());
        unionDictionary_ = new();
        idDictionary_ = new();
        vectorUnionDictionary_ = new();
        vectorIdDictionary_ = new();

        for (int i = 0; i < ObjectCount; i++)
        {
            long id = random.NextInt64();
            var choice = random.Pick(choices);

            unionDictionary_.Objects.TryAdd(id, choice);
            idDictionary_.Objects.TryAdd(id, choice.Id);
            vectorUnionDictionary_.Objects.TryAdd(new(i, i), choice);
            vectorIdDictionary_.Objects.TryAdd(new(i, i), choice.Id);
        }
        
        unionDictionaryData_ = MemoryPackSerializer.Serialize(unionDictionary_);
        idDictionaryData_ = MemoryPackSerializer.Serialize(idDictionary_);
        vectorUnionDictionaryData_ = MemoryPackSerializer.Serialize(vectorUnionDictionary_);
        vectorIdDictionaryData_ = MemoryPackSerializer.Serialize(vectorIdDictionary_);
        gameStateData_ = MemoryPackSerializer.Serialize(gameState_);
    }

    [Benchmark]
    public void DictionaryUnionSerialize()
    {
        byte[] values = MemoryPackSerializer.Serialize(unionDictionary_);
    }

    [Benchmark]
    public void DictionaryUnionDeserialize()
    { 
        var obj = MemoryPackSerializer.Deserialize<DictionaryHolderUnion>(unionDictionaryData_);
    }

    [Benchmark]
    public void DictionaryIdSerialize()
    {
        byte[] values = MemoryPackSerializer.Serialize(idDictionary_);
    }

    [Benchmark]
    public void DictionaryIdDeserialize()
    { 
        var obj = MemoryPackSerializer.Deserialize<DictionaryHolderId>(idDictionaryData_);
    }

    [Benchmark]
    public void DictionaryVectorIdSerialize()
    { 
        byte[] values = MemoryPackSerializer.Serialize(vectorIdDictionary_);
    }

    [Benchmark]
    public void DictionaryVectorIdDeserialize()
    { 
        var obj = MemoryPackSerializer.Deserialize<DictionaryHolderIdVector>(vectorIdDictionaryData_);
    }

    [Benchmark]
    public void DictionaryVectorUnionSerialize()
    { 
        byte[] values = MemoryPackSerializer.Serialize(vectorUnionDictionary_);
    }

    [Benchmark]
    public void DictionaryVectorUnionDeserialize()
    { 
        var obj = MemoryPackSerializer.Deserialize<DictionaryHolderUnionVector>(vectorUnionDictionaryData_);
    }

    [Benchmark]
    public void GameStateSerialize()
    {
        byte[] values = MemoryPackSerializer.Serialize(gameState_);
    }

    [Benchmark]
    public void GameStateDeserialize()
    { 
        var obj = MemoryPackSerializer.Deserialize<GameState>(gameStateData_);
    }
}
