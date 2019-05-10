using System;

namespace SIMDPerformanceDebug
{
    class Program
    {
        static void Main(string[] args)
        {
            bool success = true;
            FloatOps.SimpleSumArray();
            //Create a reference to compare future runs
            for(int i = 0; i < FloatOps.results.Length; i++)
            {
                FloatOps.resultsReference[i] = FloatOps.results[i];
            }
            FloatOps.SimpleSumSpan();
            success = Checkresults();
            if(!success)
            {
                Console.WriteLine("Mismatch in SimpleSumSpan");
            }
            FloatOps.SimpleSumVectors();
            success = Checkresults();
            if (!success)
            {
                Console.WriteLine("Mismatch in SimpleSumVectors");
            }
            FloatOps.SimpleSumVectorsNoCopy();
            success = Checkresults();
            if (!success)
            {
                Console.WriteLine("Mismatch in SimpleSumVectorsNoCopy");
            }
            Console.WriteLine($"Finished, no errors {success}");
            return;

            bool Checkresults()
            {
                bool opsMatch = true;
                for (int i = 0; i < FloatOps.results.Length; i++)
                {
                    opsMatch &= FloatOps.resultsReference[i] == FloatOps.results[i];
                    if (!opsMatch)
                    {
                        Console.WriteLine($"Result does not match starting at {i}: {FloatOps.resultsReference[i]} vs {FloatOps.results[i]}");
                        break;
                    }
                }
                return opsMatch;
            }
        }
    }
}
