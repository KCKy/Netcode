using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Providers;

/// <summary>
/// Provides non-deterministic server input.
/// </summary>
/// <typeparam name="TServerInput">Type of the server input.</typeparam>
public interface IServerInputProvider<TServerInput, TUpdateInfo>
{
    public TServerInput GetInput(ref TUpdateInfo info);
}
