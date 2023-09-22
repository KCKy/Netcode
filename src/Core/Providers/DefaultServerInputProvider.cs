using Core.Utility;

namespace Core.Providers;

/// <summary>
/// Provides default server input.
/// </summary>
/// <typeparam name="TServerInput">Type of the server input.</typeparam>
public class DefaultServerInputProvider<TServerInput, TUpdateInfo> : IServerInputProvider<TServerInput, TUpdateInfo> where TServerInput : class, new()
{
    public TServerInput GetInput(ref TUpdateInfo info) => DefaultProvider<TServerInput>.Create();
}
