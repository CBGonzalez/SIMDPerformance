using System;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using System.Numerics;

namespace SIMDPerformanceBench
{
    [DisassemblyDiagnoser(printAsm: true, printSource: true)]
    public class SIMDFloatPerformance
    {
        public static float[] left, right, results, resultsReference;
        public static ReadOnlyMemory<float> leftMemory, rightMemory;
        public static Memory<float> resultsMemory;
        public const int ITEMS = 100003;
        public static float floatPi;
        public static int floatSlots;

        [GlobalSetup]
        public void GlobalSetup()
        {
            floatSlots = Vector<float>.Count;
            floatPi = (float)Math.PI;
            left = new float[ITEMS];
            leftMemory = new ReadOnlyMemory<float>(left);
            right = new float[ITEMS];
            rightMemory = new ReadOnlyMemory<float>(right);
            results = new float[ITEMS];
            resultsMemory = new Memory<float>(results);
            for (int i = 0; i < ITEMS; i++)
            {
                left[i] = i;
                right[i] = i + floatPi;
            }
            resultsReference = new float[ITEMS];
        }

        [Benchmark(Baseline = true)]
        public void SimpleSumArray()
        {
            for (int i = 0; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
           
        }

        [Benchmark]
        public void SimpleSumSpan()
        {
            //results = new float[ITEMS];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;
            //resultsSpan = resultsMemory.Span;
            for (int i = 0; i < leftSpan.Length; i++)
            {
                resultsSpan[i] = leftSpan[i] + rightSpan[i];
            }
        }

        [Benchmark]
        public void SimpleSumVectors()
        {            
            int ceiling = left.Length / floatSlots * floatSlots;
            //results = new float[left.Length];
            for (int i = 0; i < ceiling; i += floatSlots)
            {
                Vector<float> v1 = new Vector<float>(left, i);
                Vector<float> v2 = new Vector<float>(right, i);
                (v1 + v2).CopyTo(results, i);
            }
            for (int i = ceiling; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
        }

        [Benchmark]
        public void SimpleSumVectorsNoCopy()
        {
            int numVectors = left.Length / floatSlots;
            int ceiling = numVectors * floatSlots;
            //results = new float[left.Length];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<Vector<float>> leftVecArray = MemoryMarshal.Cast<float, Vector<float>>(leftMemory.Span);
            ReadOnlySpan<Vector<float>> rightVecArray = MemoryMarshal.Cast<float, Vector<float>>(rightMemory.Span);
            Span<Vector<float>> resultsVecArray = MemoryMarshal.Cast<float, Vector<float>>(resultsMemory.Span);
            for (int i = 0; i < numVectors; i++)
            {
                resultsVecArray[i] = leftVecArray[i] + rightVecArray[i];
            }
            // Finish operation with any numbers leftover
            for (int i = ceiling; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
        }
    }
}
