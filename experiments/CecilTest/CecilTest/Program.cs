using System.Reflection;
using Mono.Cecil;
using MessagePack;

namespace CecilTest;

static class Program
{
    const string GameAssemblyFilename = "Game.dll";
    const string ModifiedGameAssemblyFilename = "GameModded.dll";
    const string GameStateInterfaceName = "Framework.IGameState";

    static MethodReference? ImportConstructor(this ModuleDefinition module, Type type, Type[] arguments)
    {
        var constructorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, arguments);
        return module.Import(constructorInfo);
    }

    static void Main()
    {
        var module = ModuleDefinition.ReadModule(GameAssemblyFilename);
        
        /*module.AssemblyReferences.Add(AssemblyNameReference.Parse(typeof(MessagePackSerializer).Assembly.FullName));

        var msgObjectConstructorDef = module.ImportConstructor(typeof(MessagePackObjectAttribute), new[]{typeof(bool)});

        CustomAttribute msgObjectAttr = new(msgObjectConstructorDef);

        var boolDef = module.ImportReference(typeof(bool));
        msgObjectAttr.ConstructorArguments.Add(new CustomAttributeArgument(boolDef, true));

        foreach (var type in module.Types)
        {
            var test = from i in type.Interfaces where i.InterfaceType.FullName == GameStateInterfaceName select i;

            Console.WriteLine(type.FullName);

            if (!test.Any())
                continue;

            type.CustomAttributes.Add(msgObjectAttr);
        }*/

        module.Write(ModifiedGameAssemblyFilename);
    }
}
