using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RoslynAsyncIteratorBugRepro
{
    internal class Program
    {
        // Bug repro for an issue in the C# compiler related to CancellationTokenSource disposal in async iterators
        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();

            {
                Console.WriteLine("---- IteratorCurrentImpl ----");

                var isLinked = await VerifyLinkedToken(IteratorCurrentImpl<int>.Create);
                Console.WriteLine($"IteratorCurrentImpl<int>.Create - Tokens linked correctly: {isLinked}");

                var isLinkedDiffThreads = await VerifyLinkedTokenDiffThreads(IteratorCurrentImpl<int>.Create);
                Console.WriteLine($"IteratorCurrentImpl<int>.Create - Tokens linked correctly (DIFF THREADS): {isLinkedDiffThreads}");

                var currentIterator = IteratorCurrentImpl<int>.Create(cts.Token);

                var currentIteratorType = currentIterator.GetType();
                var fields = currentIteratorType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                var combinedTokensField = fields.First(x => x.Name == "<>x__combinedTokens");

                var currentIteratorBug = await GenerateNonDisposedBug(currentIterator, () =>
                {
                    var combinedTokens = (CancellationTokenSource)combinedTokensField.GetValue(currentIterator)!;

                    return combinedTokens;
                });

                var disposedValues = GetDisposedValue(currentIteratorBug).Count(x => x);
                Console.WriteLine($"Disposed CancellationTokenSource Count of IteratorCurrentImpl<int> (MUST be 3): {disposedValues}"); // 1

                if (disposedValues != 3)
                {
                    Console.WriteLine($"The current implementation only disposed {disposedValues} out of 3");
                }

                Console.WriteLine("---- End IteratorCurrentImpl ----");
                Console.WriteLine(Environment.NewLine);
            }

            {
                Console.WriteLine("---- IteratorCurrentImplDebugFriendly ----");

                var isLinked = await VerifyLinkedToken(IteratorCurrentImplDebugFriendly<int>.Create);
                Console.WriteLine($"IteratorCurrentImplDebugFriendly<int>.Create - Tokens linked correctly: {isLinked}");

                var isLinkedDiffThreads = await VerifyLinkedTokenDiffThreads(IteratorCurrentImplDebugFriendly<int>.Create);
                Console.WriteLine($"IteratorCurrentImplDebugFriendly<int>.Create - Tokens linked correctly (DIFF THREADS): {isLinkedDiffThreads}");

                var currentIterator = IteratorCurrentImplDebugFriendly<int>.Create(cts.Token);

                var currentIteratorBug = await GenerateNonDisposedBug(currentIterator, () =>
                {
                    return ((IteratorCurrentImplDebugFriendly<int>)currentIterator)._linkedTokens!;
                });

                var disposedValues = GetDisposedValue(currentIteratorBug).Count(x => x);
                Console.WriteLine($"Disposed CancellationTokenSource Count of IteratorCurrentImplDebugFriendly<int> (MUST be 3): {disposedValues}"); // 1

                if (disposedValues != 3)
                {
                    Console.WriteLine($"The current implementation only disposed {disposedValues} out of 3");
                }

                Console.WriteLine("---- End IteratorCurrentImplDebugFriendly ----");
                Console.WriteLine(Environment.NewLine);
            }

            {
                Console.WriteLine("---- IteratorDocs ----");

                var isLinked = await VerifyLinkedToken(IteratorDocs<int>.Create);
                Console.WriteLine($"IteratorDocs<int>.Create - Tokens linked correctly: {isLinked}");

                try
                {
                    // Expected to throw NullReferenceException
                    var isLinkedDiffThreads = await VerifyLinkedTokenDiffThreads(IteratorDocs<int>.Create);
                    Console.WriteLine($"IteratorDocs<int>.Create - Tokens linked correctly (DIFF THREADS): {isLinkedDiffThreads}");

                    throw new UnreachableException();
                }
                catch (NullReferenceException)
                {
                    Console.WriteLine("""
                    Null reference on _linkedTokens

                    enumerator._cancellationToken = _linkedTokens.Token;
                                                            ^
                                                            |          
                    """);
                }
           
                var iteratorDocs = IteratorDocs<int>.Create(cts.Token);

                var iteratorDocsBug = await GenerateNonDisposedBug(iteratorDocs, () =>
                {
                    return ((IteratorDocs<int>)iteratorDocs)._linkedTokens!;
                });

                Console.WriteLine($"Disposed CancellationTokenSource Count of IteratorDocs<int> (MUST be 3): {GetDisposedValue(iteratorDocsBug).Count(x => x)}"); // 3

                Console.WriteLine("---- End IteratorDocs ----");
                Console.WriteLine(Environment.NewLine);
            }

            {
                Console.WriteLine("---- IteratorFixed ----");

                var isLinked = await VerifyLinkedToken(IteratorFixed<int>.Create);
                Console.WriteLine($"IteratorFixed<int>.Create - Tokens linked correctly: {isLinked}");

                var isLinkedDiffThreads = await VerifyLinkedTokenDiffThreads(IteratorFixed<int>.Create);
                Console.WriteLine($"IteratorFixed<int>.Create - Tokens linked correctly (DIFF THREADS): {isLinkedDiffThreads}");

                var iteratorFixed = IteratorFixed<int>.Create(cts.Token);

                var iteratorFixedBug = await GenerateNonDisposedBug(iteratorFixed, () =>
                {
                    return ((IteratorFixed<int>)iteratorFixed)._linkedTokens!;
                });

                Console.WriteLine($"Disposed CancellationTokenSource Count of IteratorFixed<int> (MUST be 3): {GetDisposedValue(iteratorFixedBug).Count(x => x)}"); // 3

                Console.WriteLine("---- End IteratorFixed ----");
                Console.WriteLine(Environment.NewLine);
            }
        }

        private static List<bool> GetDisposedValue(IEnumerable<CancellationTokenSource> sources)
        {
            var diposedField = typeof(CancellationTokenSource).GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return sources.Select(x => (bool)diposedField.GetValue(x)!).ToList();
        }

        private async static Task<List<CancellationTokenSource>> GenerateNonDisposedBug(IAsyncEnumerable<int> iterator, Func<CancellationTokenSource> getCancellationTokenSource)
        {
            var result = new List<CancellationTokenSource>();

            using var cts1 = new CancellationTokenSource();
            using var cts2 = new CancellationTokenSource();
            using var cts3 = new CancellationTokenSource();

            var it1 = iterator.GetAsyncEnumerator(cts1.Token);
            result.Add(getCancellationTokenSource());

            var it2 = iterator.GetAsyncEnumerator(cts2.Token);
            result.Add(getCancellationTokenSource());

            var it3 = iterator.GetAsyncEnumerator(cts3.Token);
            result.Add(getCancellationTokenSource());

            await it1.DisposeAsync();
            await it2.DisposeAsync();
            await it3.DisposeAsync();

            return result;
        }

        public static async Task<bool> VerifyLinkedTokenDiffThreads(Func<CancellationToken, IAsyncEnumerable<int>> createIterator)
        {
            var result1 = false;
            var result2 = false;

            {
                var ctsFirstTest = new CancellationTokenSource();
                var ctsFirstTestWithCancellation = new CancellationTokenSource();

                var createdIterator = createIterator(ctsFirstTest.Token);

                Exception? exFromThread = null;
                var thread = new Thread(async () =>
                {
                    try
                    {
                        await foreach (var num in createdIterator.WithCancellation(ctsFirstTestWithCancellation.Token))
                        {
                            ctsFirstTest.Cancel();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        result1 = true;
                    }
                    catch (Exception ex)
                    {
                        exFromThread = ex;
                    }
                });

                thread.Start();

                thread.Join();

                if (exFromThread is not null)
                {
                    throw exFromThread;
                }
            }

            {
                var ctsSecondTest = new CancellationTokenSource();
                var ctsSecondTestWithCancellation = new CancellationTokenSource();

                var createdIterator = createIterator(ctsSecondTest.Token);

                Exception? exFromThread = null;
                var thread = new Thread(async () =>
                {
                    try
                    {
                        await foreach (var num in createdIterator.WithCancellation(ctsSecondTestWithCancellation.Token))
                        {
                            ctsSecondTestWithCancellation.Cancel();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        result2 = true;
                    }
                    catch (Exception ex)
                    {
                        exFromThread = ex;
                    }
                });

                thread.Start();

                thread.Join();

                if (exFromThread is not null)
                {
                    throw exFromThread;
                }
            }

            return result1 && result2;
        }

        public static async Task<bool> VerifyLinkedToken(Func<CancellationToken, IAsyncEnumerable<int>> createIterator)
        {
            var ctsFirstTest = new CancellationTokenSource();
            var ctsFirstTestWithCancellation = new CancellationTokenSource();

            var result1 = false;
            var result2 = false;

            try
            {
                await foreach (var num in createIterator(ctsFirstTest.Token).WithCancellation(ctsFirstTestWithCancellation.Token))
                {
                    ctsFirstTest.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
                result1 = true;
            }


            var ctsSecondTest = new CancellationTokenSource();
            var ctsSecondTestWithCancellation = new CancellationTokenSource();

            try
            {
                await foreach (var num in createIterator(ctsSecondTest.Token).WithCancellation(ctsSecondTestWithCancellation.Token))
                {
                    ctsSecondTestWithCancellation.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
                result2 = true;
            }

            return result1 && result2;
        }
    }
}
