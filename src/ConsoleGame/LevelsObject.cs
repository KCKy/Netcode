using MemoryPack;

namespace TestGame;

[MemoryPackable]
[MemoryPackUnion(0, typeof(Food))]
[MemoryPackUnion(1, typeof(PlayerAvatar))]
partial interface ILevelObject
{

}

enum FoodType : byte
{
    Apple,
    Carrot
}

[MemoryPackable]
sealed partial class Food : ILevelObject
{
    [MemoryPackInclude]
    public FoodType FoodType { get; set; } = FoodType.Apple;
}
