using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RoslynAsyncIteratorBugRepro
{
    /// <summary>
    /// Iterator from async-streams.md documentation
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    public class IteratorDocs<TSource> : IAsyncEnumerable<TSource>, IAsyncEnumerator<TSource>
    {
        public static IAsyncEnumerable<TSource> Create(CancellationToken cancellationToken)
        {
            return new IteratorDocs<TSource>(cancellationToken);
        }

        private readonly int _threadId = Environment.CurrentManagedThreadId;

        private int _state;
        private TSource _current = default!;
        private CancellationToken _cancellationToken;
        public CancellationTokenSource? _linkedTokens;

        public TSource Current => _current;

        public IteratorDocs(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        public virtual ValueTask DisposeAsync()
        {
            _current = default!;
            _state = -1;
            _cancellationToken = default;

            if (_linkedTokens is not null)
            {
                _linkedTokens.Dispose();
                _linkedTokens = null;
            }

            return default;
        }

        public virtual IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken cancellationToken = default(CancellationToken))
        {
            IteratorDocs<TSource> enumerator;

            if (_state == 0 && _threadId == Environment.CurrentManagedThreadId)
            {
                enumerator = this;
            }
            else
            {
                enumerator = new(_cancellationToken);
            }
            enumerator._state = 1;

            if (_cancellationToken.Equals(default(CancellationToken)))
            {
                enumerator._cancellationToken = cancellationToken;
            }
            else if (cancellationToken.Equals(_cancellationToken) || cancellationToken.Equals(default(CancellationToken)))
            {
                enumerator._cancellationToken = _cancellationToken;
            }
            else
            {
                enumerator._linkedTokens = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);

                enumerator._cancellationToken = _linkedTokens.Token;
                //                                    ^
                //                        throws NullReferenceException when (_state == 0 && _threadId == Environment.CurrentManagedThreadId) == false
            }

            return enumerator;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            switch (_state)
            {
                case 1:
                    _state++;
                    _cancellationToken.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(true);

                case 2:
                    _state++;
                    _cancellationToken.ThrowIfCancellationRequested();
                    return ValueTask.FromResult(true);

                default:
                    return ValueTask.FromResult(false);
            }
        }
    }
}
