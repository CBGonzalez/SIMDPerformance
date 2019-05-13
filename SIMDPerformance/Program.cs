using BenchmarkDotNet.Running;

namespace SIMDPerformanceBench
{
    class Program
    {       
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<SIMDFloatPerformance>();
        }
    }
}
