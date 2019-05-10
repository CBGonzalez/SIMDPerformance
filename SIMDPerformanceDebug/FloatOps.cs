using System;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Collections.Generic;
using System.Text;

namespace SIMDPerformanceDebug
{
    public static class FloatOps
    {
        internal static float[] left, right, results, resultsReference;
        internal static ReadOnlyMemory<float> leftMemory, rightMemory;
        internal static Memory<float> resultsMemory;
        internal const int ITEMS = 100000;
        internal static float floatPi;
        internal static int floatSlots;

        static FloatOps()
        {
            floatSlots = Vector<float>.Count;
            floatPi = (float)Math.PI;
            left = new float[ITEMS];
            leftMemory = new ReadOnlyMemory<float>(left);
            right = new float[ITEMS];
            rightMemory = new ReadOnlyMemory<float>(right);
            results = new float[ITEMS];
            resultsMemory = new Memory<float>(results);
            for(int i = 0; i < ITEMS; i++)
            {
                left[i] = i;
                right[i] = i + floatPi;
            }
            resultsReference = new float[ITEMS];
        }
        
        public static ref float[] SimpleSumArray()
        {
            for(int i = 0; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }

            return ref results;
        }

        public static void SimpleSumSpan()
        {
            results = new float[ITEMS];
            resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;
            //resultsSpan = resultsMemory.Span;
            for (int i = 0; i < leftSpan.Length; i++)
            {
                resultsSpan[i] = leftSpan[i] + rightSpan[i];
            }
        }

        public static void SimpleSumVectors()
        {
            Vector<float> resultVector;
            int ceiling = left.Length / floatSlots * floatSlots;
            results = new float[left.Length];
            for (int i = 0; i < ceiling; i += floatSlots)
            {
                resultVector = new Vector<float>(left, i) + new Vector<float>(right, i);
                resultVector.CopyTo(results, i);
            }
            for (int i = ceiling; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
        }

        public static void SimpleSumVectorsNoCopy()
        {
            int numVectors = left.Length / floatSlots;
            int ceiling = numVectors * floatSlots;
            results = new float[left.Length];
            resultsMemory = new Memory<float>(results);
            ReadOnlySpan<Vector<float>> leftVecArray = MemoryMarshal.Cast<float, Vector<float>>(leftMemory.Span);
            ReadOnlySpan<Vector<float>> rightVecArray = MemoryMarshal.Cast<float, Vector<float>>(rightMemory.Span);
            Span<Vector<float>> resultsVecArray = MemoryMarshal.Cast<float, Vector<float>>(resultsMemory.Span);            
            for (int i = 0; i < numVectors; i++)
            {
                resultsVecArray[i] = leftVecArray[i] + rightVecArray[i];                
            } 
            // Finish operation with any numbers leftover
            for(int i = ceiling; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
        }
    }
}
