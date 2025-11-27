using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RoslynAsyncIteratorBugRepro
{
    /// <summary>
    /// The current implementation from the compiler
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    public class IteratorCurrentImpl<TSource>
    {
        //The current implementation does not properly dispose linked tokens
        public static async IAsyncEnumerable<TSource> Create([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return default!;

            cancellationToken.ThrowIfCancellationRequested();
            yield return default!;
        }
    }
}
