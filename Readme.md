## c# High performance SIMD operations

Some example benchmarks to demonstrate how to use vectorization with SIMD in .Net.

#### What you need ####
These projects are developed against **.NET Core 2.2** but should work also in 2.1.

In order to run the benchmarks you´ll need the excellent  [BenchmarkDotNet](https://www.nuget.org/packages/BenchmarkDotNet/).

For **.Net framework** you need to add the [System.Numerics.Vectors](https://www.nuget.org/packages/System.Numerics.Vectors/) package and [System.Memory](https://www.nuget.org/packages/System.Memory/) in order to be able to use `Span<T>` or `Memory<T>`.

For a basic **introduction to SIMD**, have a look at [this](https://github.com/CBGonzalez/SIMDIntro) project.

#### Introduction ####

SIMD (Single Instruction, Multiple Data) will be used typically to process large amounts of numeric data, where every data element needs to receive the same treatment.

Notice that SIMD happens at the processor core level, so additional speedup can be obtained using more than one thread.

On a relatively modern CPU with AVX2 capabilities, the cores are able to process vectors containing 256 bits: you can operate with **8** `float` values (8 * 32 bits = 256 bits) in one go, or **4** `double` numbers.

#### The data ####

In order to be able to create Vectors, your data needs to be available in memory, as arrays or spans.

##### Arrays #####

Imagining that your data is available in two `float[]` arrays `left` and `right` and the result will be stored in `results`, a naïve approach to summing pairs of value would be:

```
        public void SimpleSumArray()
        {
            for (int i = 0; i < left.Length; i++)
            {
                results[i] = left[i] + right[i];
            }

        }
```

It´s not really fair to measure improvement based on a worst case scenario, so an improvement on scalar performance could be to use `Span<float>` in place of naked arrays:

```
        public void SimpleSumSpan()
        {            
            ReadOnlySpan<float> leftSpan = leftMemory.Span;
            ReadOnlySpan<float> rightSpan = rightMemory.Span;
            Span<float> resultsSpan = resultsMemory.Span;            
            for (int i = 0; i < leftSpan.Length; i++)
            {
                resultsSpan[i] = leftSpan[i] + rightSpan[i];
            }
        }
```
If unsafe code is used, we can do:
```
        public unsafe void SimpleSumSpanUnsafe()
        {                    
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
```

The results on my system:

```
|              Method |      Mean |     Error |    StdDev | Ratio |
|-------------------- |----------:|----------:|----------:|------:|
|      SimpleSumArray | 225.02 us | 2.2411 us | 1.8714 us |  1.00 |
|       SimpleSumSpan | 110.40 us | 0.7707 us | 0.6017 us |  0.49 |
| SimpleSumSpanUnsafe |  74.93 us | 1.4354 us | 1.2724 us |  0.33 |
```


Using `Span<float>` instead of an array gives a nice 50 % improvement all by itself, unsafe code runs in a third of the time. The unsafe speedup is likely to disappear for more complex operations, where the access time gain through pointers fades away in face of longer operation time.

##### Vectors #####

A naïve vectorization using arrays could be:
```
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
```
And the result:
```
|              Method |      Mean |    Error |   StdDev |    Median | Ratio | RatioSD |
|-------------------- |----------:|---------:|---------:|----------:|------:|--------:|
|      SimpleSumArray | 229.84 us | 6.690 us | 5.930 us | 227.15 us |  2.02 |    0.09 |
|       SimpleSumSpan | 113.51 us | 2.557 us | 3.828 us | 112.82 us |  1.00 |    0.00 |
| SimpleSumSpanUnsafe |  75.58 us | 1.256 us | 1.114 us |  75.61 us |  0.67 |    0.02 |
|    SimpleSumVectors |  51.89 us | 1.021 us | 1.868 us |  50.99 us |  0.46 |    0.02 |
```
Already a 50% improvement over `Span` and faster that `unsafe`!

Simply using `Span<T>`in order to improve the creation of vectors doesn´t help, it actually increases the run time (not shown).

Looking at the `SimpleSumVectors()` code above two things jump out: we need to repeatedly create vectors and we need to copy the resulting values back to the result array, and all that inside the inner loop.

Let´s try to avoid that using `System.Runtime.InteropservicesMemoryMarshal.Cast`. This function will allow us to map data from one type to another, without actually copying bytes around.

```
        public void SimpleSumVectorsNoCopy()
        {
            int numVectors = left.Length / floatSlots;
            int ceiling = numVectors * floatSlots;

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
```
The magic happens by using `MemoryMarshal.Cast<float, Vector<float>>(leftMemory.Span)`: the `float` array in `lefMemory.Span` is reinterpreted as an array of `Vector<float>`.

Remember that a `lefMemory.Span` actually points to the array we used to create it. In the same way, `leftVecArray` point to the same data. So if data changes in `resultsVecArray`, the array `results` actually gets changed.

The result of that magic:
```
|                 Method |      Mean |     Error |    StdDev |    Median | Ratio | RatioSD |
|----------------------- |----------:|----------:|----------:|----------:|------:|--------:|
|         SimpleSumArray | 239.42 us | 4.7123 us | 9.3016 us | 235.43 us |  2.00 |    0.12 |
|          SimpleSumSpan | 119.90 us | 7.9034 us | 8.1162 us | 115.94 us |  1.00 |    0.00 |
|    SimpleSumSpanUnsafe |  79.21 us | 1.5772 us | 1.9370 us |  78.95 us |  0.66 |    0.04 |
|       SimpleSumVectors |  53.93 us | 1.0742 us | 2.3123 us |  53.03 us |  0.46 |    0.04 |
| SimpleSumVectorsNoCopy |  44.07 us | 0.4126 us | 0.3445 us |  43.98 us |  0.37 |    0.03 |
```
We achieve a bit less than a 50% improvement over unsafe operations and a respectable improvement over `Span` operations.

##### More complex calculations #####

If we replace the simple sum

```
results[i] = left[i] + right[i];
```
 with
 ```
results[i] = (float)Math.Sqrt((left[i] * right[i] + floatPi) / floatPi);
 ```
we see a more substantial gain for vectorization (see the project for code):
```
|               Method |     Mean |      Error |     StdDev | Ratio | RatioSD |
|--------------------- |---------:|-----------:|-----------:|------:|--------:|
|       ComplexOpsSpan | 754.9 us | 11.2312 us | 10.5057 us |  1.00 |    0.00 |
| ComplexOpsSpanUnsafe | 753.8 us |  9.4597 us |  8.8486 us |  1.00 |    0.02 |
| ComplexVectorsNoCopy | 133.2 us |  0.4963 us |  0.4400 us |  0.18 |    0.00 |
```
We have a very respectable > 5x improvement in performance (and, as expected, the advantage of doing unsafe operations disappears).

#### Conclusion ####

If you are going to vectorize your calculations, **benchmarking is a must** to make sure you´re actually improving performance.

A case in point is integer types: only addition, subtraction and bitwise operations are supported.

An example using division and `Vector.Sqrt` gives the following results:
```
|                     Method |       Mean |      Error |     StdDev |
|--------------------------- |-----------:|-----------:|-----------:|
|          ComplexOpsIntSpan |   364.7 us |  5.4188 us |  4.8036 us |
| ComplexOpsVectorsNoCopyInt | 1,020.7 us | 35.6401 us | 33.3378 us |
```
The vectorized routine **increases** execution time 2.8x since the vectorized code is compiled to use software implementations instead of hardware operations.
