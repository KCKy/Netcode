using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Kcky.Useful;

/// <summary>
/// Extensions which provide awaitability to <see cref="CancellationToken"/>.
/// </summary>
/// <example>
/// <code>
/// CancellationToken token = ...;
/// await token; // Wait until token is cancelled.
/// </code>
/// </example>
/// <remarks>
/// This was adapted from https://medium.com/@cilliemalan/how-to-await-a-cancellation-token-in-c-cbfc88f28fa2.
/// </remarks>
public static class AsyncExtensions
{
#pragma warning disable CS1591
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CancellationTokenAwaiter GetAwaiter(this CancellationToken ct)
    {
        return new()
        {
            CancellationToken = ct
        };
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct CancellationTokenAwaiter : ICriticalNotifyCompletion
    {
        public CancellationTokenAwaiter(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        internal CancellationToken CancellationToken;

        public readonly object GetResult()
        {
            if (IsCompleted)
                throw new OperationCanceledException();
            
            throw new InvalidOperationException();
        }

        public readonly bool IsCompleted => CancellationToken.IsCancellationRequested;

        public readonly void OnCompleted(Action continuation) => CancellationToken.Register(continuation);
        public readonly void UnsafeOnCompleted(Action continuation) => CancellationToken.Register(continuation);
    }
#pragma warning restore CS1591
}
