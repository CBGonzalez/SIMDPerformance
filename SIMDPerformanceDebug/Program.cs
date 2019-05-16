using System;

namespace SIMDPerformanceDebug
{
    class Program
    {
        static void Main(string[] args)
        {
            bool success = true, overallSuccess = true;
            FloatOps.SimpleSumArray();
            //Create a reference to compare future runs
            for(int i = 0; i < FloatOps.results.Length; i++)
            {
                FloatOps.resultsReference[i] = FloatOps.results[i];
            }
            FloatOps.SimpleSumSpan();
            success = Checkresults();
            overallSuccess &= success;
            if(!success)
            {
                Console.WriteLine("Mismatch in SimpleSumSpan");
            }
            FloatOps.SimpleSumSpanUnsafe();
            if (!success)
            {
                Console.WriteLine("Mismatch in SimpleSumSpanUnsafe");
            }
            success = Checkresults();
            overallSuccess &= success;
            if (!success)
            {
                Console.WriteLine("Mismatch in SimpleSumVectors");
            }
            FloatOps.SimpleSumVectors();
            success = Checkresults();
            overallSuccess &= success;
            if (!success)
            {
                Console.WriteLine("Mismatch in SimpleSumVectors");
            }
            FloatOps.SimpleSumVectorsNoCopy();
            success = Checkresults();
            overallSuccess &= success;
            if (!success)
            {
                Console.WriteLine("Mismatch in SimpleSumVectorsNoCopy");
            }
            FloatOps.SimpleSumVectorsUnsafe();
            for(int i = 0; i < FloatOps.resultsUnsafe.Length; i++)
            {
                success = true;
                if (FloatOps.resultsUnsafe[i] != FloatOps.resultsReference[i])
                {
                    Console.WriteLine($"Result does not match starting at {i}: {FloatOps.resultsReference[i]} vs {FloatOps.results[i]}");
                    success = false;
                    break;
                }
            }
            overallSuccess &= success;
            Console.WriteLine($"Finished. Success: {overallSuccess}");
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
