using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;

namespace BenchmarkSuite1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, DefaultConfig.Instance);
        }
    }
}
