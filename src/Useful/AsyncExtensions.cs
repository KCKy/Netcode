using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Useful;

// SOURCE: https://medium.com/@cilliemalan/how-to-await-a-cancellation-token-in-c-cbfc88f28fa2

public static class AsyncExtensions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static CancellationTokenAwaiter GetAwaiter(this CancellationToken ct)
    {
        return new CancellationTokenAwaiter
        {
            CancellationToken = ct
        };
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct CancellationTokenAwaiter : INotifyCompletion, ICriticalNotifyCompletion
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
}
