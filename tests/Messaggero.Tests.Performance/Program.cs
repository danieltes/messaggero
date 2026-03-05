using BenchmarkDotNet.Running;

namespace Messaggero.Tests.Performance;

/// <summary>
/// Entry point for BenchmarkDotNet performance tests.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all benchmarks in the assembly.
    /// </summary>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
