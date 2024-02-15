using System;

namespace Core.Utility;

interface IStateHolder<TC, TS, TG>
    where TC : class, new()
    where TS : class, new()
{
    UpdateOutput Update(UpdateInput<TC, TS> input);
    TG State { get; set; }
    long Frame { get; set; }
    long GetChecksum();
    Memory<byte> Serialize();
}
