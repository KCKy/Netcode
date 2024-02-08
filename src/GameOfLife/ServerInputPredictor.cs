using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Providers;

namespace GameOfLife
{
    class ServerInputPredictor : IServerInputPredictor<ServerInput, GameState>
    {
        public void PredictInput(ref ServerInput input, GameState info)
        {
            input.CellRespawnEventSeed = 0;
        }
    }
}
