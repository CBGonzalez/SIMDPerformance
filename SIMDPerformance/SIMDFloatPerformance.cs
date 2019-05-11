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
        UnsafeMemoryFloat leftUnsafe, rightUnsafe, resultsUnsafe;
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
            leftUnsafe = new UnsafeMemoryFloat(ITEMS, Vector<byte>.Count, 0);
            rightUnsafe = new UnsafeMemoryFloat(ITEMS, Vector<byte>.Count, 0);
            resultsUnsafe = new UnsafeMemoryFloat(ITEMS, Vector<byte>.Count, 0);
            for (int i = 0; i < ITEMS; i++)
            {
                left[i] = i;
                right[i] = i + floatPi;
                leftUnsafe[i] = i;
                rightUnsafe[i] = i + floatPi;
            }
            resultsReference = new float[ITEMS];
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if(leftUnsafe != null)
            {
                leftUnsafe.Dispose();
                leftUnsafe = null;
            }
            if (rightUnsafe != null)
            {
                rightUnsafe.Dispose();
                rightUnsafe = null;
            }
            if (resultsUnsafe != null)
            {
                resultsUnsafe.Dispose();
                resultsUnsafe = null;
            }
        }


        //[Benchmark]
        public void SimpleSumArray()
        {
            for (int i = 0; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
           
        }

        //[Benchmark(Baseline = true)]
        public void SimpleSumSpan()
        {
            //results = new float[ITEMS];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;            
            for (int i = 0; i < leftSpan.Length; i++)
            {
                resultsSpan[i] = leftSpan[i] + rightSpan[i];
            }
        }

        //[Benchmark]
        public unsafe void SimpleSumSpanUnsafe()
        {            
            //results = new float[ITEMS];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;                
            fixed (float* leftBasePtr = &leftSpan[0])
            fixed (float* rightBasePtr = &rightSpan[0])
            fixed (float* resultBasePtr = &resultsSpan[0])
            {
                float* leftCurrPtr = leftBasePtr;
                float* rightCurrPtr = rightBasePtr;
                float* resultCurrPtr = resultBasePtr;
                for (int i = 0; i < leftSpan.Length; i++)
                {
                    *resultCurrPtr = *leftCurrPtr + *rightCurrPtr;
                    rightCurrPtr++;
                    leftCurrPtr++;
                    resultCurrPtr++;
                }
            }            
        }        

        //[Benchmark]
        public void SimpleSumVectors()
        {
            int ceiling = left.Length / floatSlots * floatSlots;
            
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

        //[Benchmark]
        public void SimpleSumVectorsSpan()
        {
            int ceiling = left.Length / floatSlots * floatSlots;
            Span<float> leftSpan = new Span<float>(left);
            Span<float> rightSpan = new Span<float>(right);
            //Span<float> resultsSpan = resultsMemory.Span;
            Span<float> leftSlice = leftSpan.Slice(0, floatSlots);
            Span<float> rightSlice = rightSpan.Slice(0, floatSlots);
            //Span<float> resultSlice = resultsSpan.Slice(0, floatSlots);
            //results = new float[left.Length];
            for (int i = 0; i < ceiling; i += floatSlots)
            {
                Vector<float> v1 = new Vector<float>(leftSlice);
                Vector<float> v2 = new Vector<float>(rightSlice);
                leftSlice = leftSpan.Slice(i, floatSlots);
                rightSlice = rightSpan.Slice(i, floatSlots);
                (v1 + v2).CopyTo(results, i);
            }
            // Finish operation with any numbers leftover
            for (int i = ceiling; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
        }

        //[Benchmark]
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

        //[Benchmark]
        public unsafe void SimpleSumVectorsUnsafe()
        {
            int numVectors = left.Length / floatSlots;
            int ceiling = numVectors * floatSlots;
            ReadOnlySpan<float> leftUnsafeSpan = new ReadOnlySpan<float>(leftUnsafe.BufferIntPtr.ToPointer(), numVectors * floatSlots);
            ReadOnlySpan<float> rightUnsafeSpan = new ReadOnlySpan<float>(rightUnsafe.BufferIntPtr.ToPointer(), numVectors * floatSlots);
            Span<float> resultsUnsafeSpan = new Span<float>(resultsUnsafe.BufferIntPtr.ToPointer(), numVectors * floatSlots);
            ReadOnlySpan<Vector<float>> leftVecArray = MemoryMarshal.Cast<float, Vector<float>>(leftUnsafeSpan);
            ReadOnlySpan<Vector<float>> rightVecArray = MemoryMarshal.Cast<float, Vector<float>>(rightUnsafeSpan);
            Span<Vector<float>> resultsVecArray = MemoryMarshal.Cast<float, Vector<float>>(resultsUnsafeSpan);
            for (int i = 0; i < numVectors; i++)
            {
                resultsVecArray[i] = leftVecArray[i] + rightVecArray[i];
            }
            for (int i = ceiling; i < left.Length; i++)
            {
                //resultsUnsafe[i] = leftUnsafe[i] + rightUnsafe[i];
                results[i] = left[i] + right[i];
            }
        }

        [Benchmark(Baseline = true)]
        public void ComplexOpsSpan()
        {
            //results = new float[ITEMS];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;
            for (int i = 0; i < leftSpan.Length; i++)
            {
                resultsSpan[i] = (float)Math.Sqrt((leftSpan[i] * rightSpan[i] + floatPi) / floatPi);
            }
        }

        [Benchmark]
        public unsafe void ComplexOpsSpanUnsafe()
        {
            //results = new float[ITEMS];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;
            fixed (float* leftBasePtr = &leftSpan[0])
            fixed (float* rightBasePtr = &rightSpan[0])
            fixed (float* resultBasePtr = &resultsSpan[0])
            {
                float* leftCurrPtr = leftBasePtr;
                float* rightCurrPtr = rightBasePtr;
                float* resultCurrPtr = resultBasePtr;
                for (int i = 0; i < leftSpan.Length; i++)
                {
                    *resultCurrPtr = (float)Math.Sqrt((*leftCurrPtr * *rightCurrPtr + floatPi) / floatPi);
                    rightCurrPtr++;
                    leftCurrPtr++;
                    resultCurrPtr++;
                }
            }
        }

        [Benchmark]
        public void ComplexVectorsNoCopy()
        {
            int numVectors = left.Length / floatSlots;
            int ceiling = numVectors * floatSlots;
            Vector<float> piVector = new Vector<float>(floatPi);
            //results = new float[left.Length];
            //resultsMemory = new Memory<float>(results);
            ReadOnlySpan<Vector<float>> leftVecArray = MemoryMarshal.Cast<float, Vector<float>>(leftMemory.Span);
            ReadOnlySpan<Vector<float>> rightVecArray = MemoryMarshal.Cast<float, Vector<float>>(rightMemory.Span);
            Span<Vector<float>> resultsVecArray = MemoryMarshal.Cast<float, Vector<float>>(resultsMemory.Span);
            for (int i = 0; i < numVectors; i++)
            {
                resultsVecArray[i] = Vector.SquareRoot((leftVecArray[i] * rightVecArray[i] + piVector) / piVector);
            }
            // Finish operation with any numbers leftover
            for (int i = ceiling; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }
        }
    }

    public unsafe class UnsafeMemoryFloat : IDisposable
    {
        private byte[] byteBuffer;
        private GCHandle bufferGCHandle;
        private readonly IntPtr bufferIntPtr;
        private readonly int length;

        public float this[int index]
        {
            get { return *((float*)bufferIntPtr.ToPointer() + index); }
            set { *((float*)bufferIntPtr.ToPointer() + index) = value; }
        }
        
        private bool disposedValue = false;

        public int Length => length;
        public IntPtr BufferIntPtr => bufferIntPtr;

        public UnsafeMemoryFloat(int len, int byteAlignment, int offset)
        {
            length = len;
            byteBuffer = new byte[length * sizeof(float) + byteAlignment];
            bufferGCHandle = GCHandle.Alloc(byteBuffer, GCHandleType.Pinned);
            long int64Ptr = bufferGCHandle.AddrOfPinnedObject().ToInt64();
            long alignError = byteAlignment - int64Ptr % byteAlignment;
            int64Ptr = int64Ptr + alignError;
            int64Ptr += offset;
            bufferIntPtr = new IntPtr(int64Ptr);
        }

        

        #region IDisposable Support        
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (bufferGCHandle.IsAllocated)
                    {
                        bufferGCHandle.Free();
                        byteBuffer = null;
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
