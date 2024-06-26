﻿using System.Threading;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Transport.Default;

interface ISendProtocol<in TOut>
{
    ValueTask SendAsync(TOut data, CancellationToken cancellation);
}

interface IReceiveProtocol<TIn>
{
    ValueTask<TIn> ReceiveAsync(CancellationToken cancellation);
}

interface IProtocol<TIn, in TOut> : IReceiveProtocol<TIn>, ISendProtocol<TOut> { }
