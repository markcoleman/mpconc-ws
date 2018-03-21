﻿using CommonHelpers;
using Parallelism;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Parallelism.Data;

namespace ParallelizingFuzzyMatch
{
    // TODO : 1.8
    public class ProcessFuzzyMacth
    {
        static void Log(string match)  {}// Console.Write("{0}\t", match);

        public static void SequentialFuzzyMatch()
        {
            List<string> matches = new List<string>();
            BenchPerformance.Time("Sequential Fuzzy Match", iterations: Data.Iterations, operation: () =>
            {
                foreach (var word in WordsToSearch)
                {
                    var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, word);
                    matches.AddRange(localMathes.Select(m => m.Word));
                }
            });
            foreach (var match in matches.Distinct())
            {
                Log(match);
            }
            Console.WriteLine();
        }

        public static void LinqFuzzyMatch()
        {
            BenchPerformance.Time("Linq Fuzzy Match", () =>
            {
                var matches = (from word in WordsToSearch
                               from match in FuzzyMatch.JaroWinklerModule.bestMatch(Words, word)
                               select match.Word);

                foreach (var match in matches)
                {
                    Log(match);
                }
            });
        }

        public static void ThreadFuzzyMatch()
        {
            List<string> matches = new List<string>();
            BenchPerformance.Time("Thread Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    var t = new Thread(() =>
                    {
                        foreach (var word in WordsToSearch)
                        {
                            var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, word);
                            matches.AddRange(localMathes.Select(m => m.Word));
                        }
                    });
                    t.Start();
                    t.Join();

                });
            foreach (var match in matches.Distinct())
            {
                Log(match);
            }
        }

        public static void TwoThreadsFuzzyMatch()
        {
            List<string> matches = new List<string>();
            BenchPerformance.Time("Two Thread Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    var t1 = new Thread(() =>
                    {
                        var take = WordsToSearch.Count / 2;
                        var start = 0;

                        foreach (var word in WordsToSearch.Take(take))
                        {
                            var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, word);
                            matches.AddRange(localMathes.Select(m => m.Word));
                        }
                    });
                    var t2 = new Thread(() =>
                    {
                        var start = WordsToSearch.Count / 2;
                        var take = WordsToSearch.Count - start;

                        foreach (var word in WordsToSearch.Skip(start).Take(take))
                        {
                            var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, word);
                            matches.AddRange(localMathes.Select(m => m.Word));
                        }
                    });
                    t1.Start();
                    t2.Start();
                    t1.Join();
                    t2.Join();
                });
            foreach (var match in matches.Distinct())
            {
                Log(match);
            }
        }

        public static void MultipleThreadsFuzzyMatch()
        {
            List<string> matches = new List<string>();
            BenchPerformance.Time("Multi Thread Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    var threads = new Thread[Environment.ProcessorCount];

                    for (int i = 0; i < threads.Length; i++)
                    {
                        var index = i;
                        threads[index] = new Thread(() =>
                        {
                            var take = WordsToSearch.Count / (Math.Min(WordsToSearch.Count, threads.Length));
                            var start = index == threads.Length - 1 ? WordsToSearch.Count - take : index * take;
                            foreach (var word in WordsToSearch.Skip(start).Take(take))
                            {
                                var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, word);
                                matches.AddRange(localMathes.Select(m => m.Word));
                            }
                        });
                    }

                    for (int i = 0; i < threads.Length; i++)
                        threads[i].Start();
                    for (int i = 0; i < threads.Length; i++)
                        threads[i].Join();
                });
            foreach (var match in matches.Distinct())
            {
                Log(match);
            }
        }

        public static void MultipleTasksFuzzyMatch()
        {
            var tasks = new List<Task<List<string>>>();
            var matches = new List<string>();
            BenchPerformance.Time("Multi Tasks Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    foreach (var word in WordsToSearch)
                    {
                        tasks.Add(Task.Factory.StartNew<List<string>>((w) =>
                        {
                            List<string> localMatches = new List<string>();
                            var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, (string)w);
                            localMatches.AddRange(localMathes.Select(m => m.Word));
                            return localMatches;
                        }, word));
                    }

                    Task.Factory.ContinueWhenAll(tasks.ToArray(), (ts) =>
                    {
                        matches = new List<string>(tasks.SelectMany(t => t.Result).Distinct());
                    }).Wait();
                });
            foreach (var match in matches)
            {
                Log(match);
            }
            Console.WriteLine();
        }


        // TODO
        // (1) implement a fast fuzzy match
        // you can use either PLINQ and/or Parallel loop. The latter requires attention to avoid race condition


        #region Solution

        public static void ParallelLoopFuzzyMatch()
        {
            List<string> matches = new List<string>();
            BenchPerformance.Time("Parallel Loop Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    object sync = new object();

                    Parallel.ForEach(WordsToSearch,
                                        // thread local initializer
                                        () => { return new List<string>(); },
                                        (word, loopState, localMatches) =>
                                        {
                                            var localMathes = FuzzyMatch.JaroWinklerModule.bestMatch(Words, word);
                                            localMatches.AddRange(localMathes.Select(m => m.Word));// same code
                                            return localMatches;
                                        },
                        (finalResult) =>
                        {
                            // thread local aggregator
                            lock (sync) matches.AddRange(finalResult);
                        }
                    );
                });

            foreach (var match in matches.Distinct())
            {
                Log(match);
            }
        }

        public static void ParallelLinqFuzzyMatch()
        {
            BenchPerformance.Time("Parallel Linq Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    ParallelQuery<string> matches = (from word in WordsToSearch.AsParallel()
                                                     from match in FuzzyMatch.JaroWinklerModule.bestMatch(Words, word)
                                                     select match.Word);
                    matches.ForAll(match =>
                    {
                        Log(match);
                    });
                });
        }

        public static void ParallelLinqPartitionerFuzzyMatch()
        {
            BenchPerformance.Time("Parallel PLinq  partitioner Fuzzy Match",
                iterations: Data.Iterations, operation: () =>
                {
                    var partitioner = Partitioner.Create(WordsToSearch, EnumerablePartitionerOptions.NoBuffering);

                    ParallelQuery<string> matches = (from word in WordsToSearch.AsParallel()
                                                     from match in FuzzyMatch.JaroWinklerModule.bestMatch(Words, word)
                                                     select match.Word);
                    matches.ForAll(match =>
                    {
                        Log(match);
                    });
                });
        }
        #endregion
    }
}

