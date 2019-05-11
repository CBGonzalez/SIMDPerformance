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
        internal static UnsafeMemoryFloat leftUnsafe, rightUnsafe, resultsUnsafe;
        internal static Memory<float> resultsMemory;
        internal const int ITEMS = 100003;
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

        public static unsafe void SimpleSumSpanUnsafe()
        {
            results = new float[ITEMS];
            resultsMemory = new Memory<float>(results);
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;
            //resultsSpan = resultsMemory.Span;
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

        public static unsafe void SimpleSumVectorsUnsafe()
        {
            int numVectors = left.Length / floatSlots;
            ReadOnlySpan<float> leftUnsafeSpan = new ReadOnlySpan<float>(leftUnsafe.BufferIntPtr.ToPointer(), numVectors * floatSlots);
            ReadOnlySpan<float> rightUnsafeSpan = new ReadOnlySpan<float>(rightUnsafe.BufferIntPtr.ToPointer(), numVectors * floatSlots);
            Span<float> resultsUnsafeSpan = new Span<float>(resultsUnsafe.BufferIntPtr.ToPointer(), numVectors * floatSlots);
            ReadOnlySpan<Vector<float>> leftVecArray = MemoryMarshal.Cast<float, Vector<float>>(leftUnsafeSpan);
            ReadOnlySpan<Vector<float>> rightVecArray = MemoryMarshal.Cast<float, Vector<float>>(rightUnsafeSpan);
            Span<Vector<float>> resultsVecArray = MemoryMarshal.Cast<float, Vector<float>>(resultsUnsafeSpan);
            
            for(int i = 0; i < numVectors; i++)
            {
                resultsVecArray[i] = leftVecArray[i] + rightVecArray[i];
            }
            for(int i = numVectors * floatSlots; i < left.Length; i++)
            {
                resultsUnsafe[i] = leftUnsafe[i] + rightUnsafe[i];
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
            get => *((float*)bufferIntPtr.ToPointer() + index);
            set => *((float*)bufferIntPtr.ToPointer() + index) = value;
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
            int64Ptr += alignError;
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
