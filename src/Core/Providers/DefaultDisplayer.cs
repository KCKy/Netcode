using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Providers;

public class DefaultDisplayer<TGameState> : IDisplayer<TGameState>
{
    public void AddAuthoritative(long frame, TGameState gameState) { }
    public void AddPredict(long frame, TGameState gameState) { }
}
