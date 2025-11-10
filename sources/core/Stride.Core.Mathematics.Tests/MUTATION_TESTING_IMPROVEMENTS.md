# Mutation Testing Analysis & Test Improvements for Stride.Core.Mathematics

## Executive Summary

After analyzing the Stride.Core.Mathematics library and its test suite using mutation testing principles, several areas for test improvement have been identified. While Stryker.NET encountered technical challenges with the complex Stride build system, manual analysis has revealed specific weaknesses in the current test coverage.

## Stryker Configuration Issues

The following Stryker configuration was created but encountered build system compatibility issues:

**Location:** `sources/core/Stride.Core.Mathematics.Tests/stryker-config.json`

**Issue:** All 12,211 created mutants were filtered out due to the complex MSBuild variable resolution in the Stride project structure.

## Key Findings & Recommended Test Improvements

### 1. **Boundary Condition Testing for `Clamp` Functions**

**Current Issue:** Tests don't verify that the comparison operators are correct.

**Potential Surviving Mutants:**
- Changing `<` to `<=` or `>` to `>=` in the clamp logic
- Swapping min and max comparisons

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(0, 0, 10, 0)]  // Test exact min boundary
[InlineData(10, 0, 10, 10)] // Test exact max boundary
[InlineData(float.NegativeInfinity, 0, 10, 0)]
[InlineData(float.PositiveInfinity, 0, 10, 10)]
[InlineData(float.NaN, 0, 10, float.NaN)] // Edge case
public void TestClampEdgeCases(float value, float min, float max, float expected)
{
    var result = MathUtil.Clamp(value, min, max);
    if (float.IsNaN(expected))
        Assert.True(float.IsNaN(result));
    else
        Assert.Equal(expected, result);
}

[Fact]
public void TestClampMinEqualsMax()
{
    // Test when min == max
    Assert.Equal(5.0f, MathUtil.Clamp(3.0f, 5.0f, 5.0f));
    Assert.Equal(5.0f, MathUtil.Clamp(7.0f, 5.0f, 5.0f));
    Assert.Equal(5.0f, MathUtil.Clamp(5.0f, 5.0f, 5.0f));
}
```

### 2. **InverseLerp Division by Zero Handling**

**Current Issue:** The test doesn't verify that NaN is returned when min == max.

**Code Under Test:**
```csharp
public static float InverseLerp(float min, float max, float value)
{
    if (IsZero(Math.Abs(max - min)))
        return float.NaN;
    return (value - min) / (max - min);
}
```

**Potential Surviving Mutants:**
- Changing `IsZero` condition
- Changing return value from `float.NaN` to something else
- Removing the check entirely

**Recommended Additional Tests:**

```csharp
[Fact]
public void TestInverseLerpDivisionByZero()
{
    // When min == max, should return NaN
    var result = MathUtil.InverseLerp(5.0f, 5.0f, 3.0f);
    Assert.True(float.IsNaN(result), "InverseLerp should return NaN when min equals max");
}

[Fact]
public void TestInverseLerpNearlyEqual()
{
    // When min and max are very close (within zero tolerance)
    var result = MathUtil.InverseLerp(5.0f, 5.0f + MathUtil.ZeroTolerance * 0.5f, 5.0f);
    Assert.True(float.IsNaN(result), "InverseLerp should return NaN when min and max are within zero tolerance");
}

[Theory]
[InlineData(0.0, 10.0, -5.0, -0.5)]  // Value below range
[InlineData(0.0, 10.0, 15.0, 1.5)]   // Value above range
public void TestInverseLerpOutOfRange(double min, double max, double value, double expected)
{
    Assert.Equal(expected, MathUtil.InverseLerp(min, max, value), 5);
}
```

### 3. **SmoothStep and SmootherStep Boundary Testing**

**Current Issue:** Tests don't verify the exact behavior at boundaries and the specific mathematical formula.

**Potential Surviving Mutants:**
- Changing comparison operators (`<=` to `<`)
- Modifying the polynomial coefficients
- Changing the boundary return values

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(-0.1f, 0.0f)]  // Below range should return 0
[InlineData(1.1f, 1.0f)]   // Above range should return 1
[InlineData(-100.0f, 0.0f)]
[InlineData(100.0f, 1.0f)]
public void TestSmoothStepOutOfBounds(float amount, float expected)
{
    Assert.Equal(expected, MathUtil.SmoothStep(amount), 5);
}

[Fact]
public void TestSmoothStepFormula()
{
    // Verify the actual formula: x^2 * (3 - 2x)
    float amount = 0.3f;
    float expected = amount * amount * (3 - (2 * amount));
    Assert.Equal(expected, MathUtil.SmoothStep(amount), 5);
}

[Theory]
[InlineData(-0.1f, 0.0f)]
[InlineData(1.1f, 1.0f)]
public void TestSmootherStepOutOfBounds(float amount, float expected)
{
    Assert.Equal(expected, MathUtil.SmootherStep(amount), 5);
}

[Fact]
public void TestSmootherStepFormula()
{
    // Verify the actual formula: x^3 * (x * (x * 6 - 15) + 10)
    float amount = 0.3f;
    float expected = amount * amount * amount * ((amount * ((amount * 6) - 15)) + 10);
    Assert.Equal(expected, MathUtil.SmootherStep(amount), 5);
}

[Fact]
public void TestSmootherStepDerivativesAtBoundaries()
{
    // SmootherStep should have zero 1st and 2nd derivatives at 0 and 1
    // We can test this by checking nearby values
    float epsilon = 0.001f;
    
    // Near 0
    var val0 = MathUtil.SmootherStep(0);
    var valEps = MathUtil.SmootherStep(epsilon);
    // First derivative should be close to 0
    var derivative = (valEps - val0) / epsilon;
    Assert.True(Math.Abs(derivative) < 0.01f, "First derivative at 0 should be near 0");
}
```

### 4. **NearEqual Precision Testing**

**Current Issue:** Limited testing of the ULP (Unit in the Last Place) comparison logic.

**Potential Surviving Mutants:**
- Changing the `maxUlp` constant from 4 to another value
- Modifying the sign comparison logic
- Changing the ULP calculation

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(1.0f, 1.0f + float.Epsilon * 3, true)]  // Within 4 ULPs
[InlineData(1.0f, 1.0f + float.Epsilon * 5, false)] // Beyond 4 ULPs (potentially)
[InlineData(-1.0f, 1.0f, false)] // Different signs
[InlineData(-1.0f, -1.0f, true)] // Same negative values
[InlineData(0.0f, -0.0f, true)]  // Positive and negative zero
public void TestNearEqualULPPrecision(float a, float b, bool expected)
{
    Assert.Equal(expected, MathUtil.NearEqual(a, b));
}

[Fact]
public void TestNearEqualZeroValues()
{
    // Values very close to zero should use IsZero logic
    Assert.True(MathUtil.NearEqual(0.0f, MathUtil.ZeroTolerance * 0.5f));
    Assert.True(MathUtil.NearEqual(MathUtil.ZeroTolerance * 0.5f, 0.0f));
    Assert.False(MathUtil.NearEqual(0.0f, MathUtil.ZeroTolerance * 2.0f));
}

[Theory]
[InlineData(float.PositiveInfinity, float.PositiveInfinity, true)]
[InlineData(float.NegativeInfinity, float.NegativeInfinity, true)]
[InlineData(float.PositiveInfinity, float.NegativeInfinity, false)]
[InlineData(float.NaN, float.NaN, false)] // NaN != NaN
[InlineData(float.NaN, 1.0f, false)]
public void TestNearEqualSpecialValues(float a, float b, bool expected)
{
    Assert.Equal(expected, MathUtil.NearEqual(a, b));
}
```

### 5. **IsInRange Boundary Inclusivity**

**Current Issue:** Need to verify that the range is truly inclusive on both ends.

**Potential Surviving Mutants:**
- Changing `<=` to `<` or vice versa

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(0, 0, 0, true)]   // min == max == value
[InlineData(5, 5, 5, true)]   // min == max == value (non-zero)
[InlineData(0, 0, 1, false)]  // value > max when min == max
[InlineData(1, 0, 0, false)]  // value > max when min == max
public void TestIsInRangeIntEdgeCases(int value, int min, int max, bool expected)
{
    Assert.Equal(expected, MathUtil.IsInRange(value, min, max));
}

[Fact]
public void TestIsInRangeFloatInvertedRange()
{
    // What happens when min > max? (undefined behavior, but should be tested)
    // This tests the implementation's actual behavior
    var result = MathUtil.IsInRange(5.0f, 10.0f, 0.0f);
    Assert.False(result, "Value should not be in inverted range");
}
```

### 6. **IsPow2 Edge Cases**

**Current Issue:** Missing tests for negative numbers and extreme values.

**Potential Surviving Mutants:**
- Changing the zero check
- Modifying the bitwise operation

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(-1, false)]
[InlineData(-2, false)]
[InlineData(-4, false)]
[InlineData(int.MaxValue, false)]
[InlineData(int.MinValue, false)]
[InlineData(1073741824, true)] // 2^30, large power of 2
public void TestIsPow2EdgeCases(int value, bool expected)
{
    Assert.Equal(expected, MathUtil.IsPow2(value));
}

[Fact]
public void TestIsPow2AllPowersOfTwo()
{
    // Test all powers of 2 from 2^0 to 2^30
    for (int i = 0; i <= 30; i++)
    {
        int value = 1 << i;
        Assert.True(MathUtil.IsPow2(value), $"2^{i} = {value} should be power of 2");
    }
}

[Fact]
public void TestIsPow2NonPowersNearPowers()
{
    // Test values near powers of 2
    for (int i = 1; i <= 10; i++)
    {
        int power = 1 << i;
        Assert.False(MathUtil.IsPow2(power - 1), $"{power - 1} should not be power of 2");
        Assert.False(MathUtil.IsPow2(power + 1), $"{power + 1} should not be power of 2");
    }
}
```

### 7. **WithinEpsilon Symmetry and Edge Cases**

**Current Issue:** Tests don't verify symmetry and boundary behavior.

**Potential Surviving Mutants:**
- Changing `-epsilon` to `epsilon` or vice versa
- Modifying the comparison operators

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(1.0f, 1.1f, 0.1f, true)]  // Exactly at epsilon boundary
[InlineData(1.0f, 1.1f, 0.09999f, false)] // Just below epsilon
[InlineData(1.0f, 0.9f, 0.1f, true)]  // Negative difference
public void TestWithinEpsilonBoundaries(float a, float b, float epsilon, bool expected)
{
    Assert.Equal(expected, MathUtil.WithinEpsilon(a, b, epsilon));
}

[Fact]
public void TestWithinEpsilonSymmetry()
{
    // WithinEpsilon(a, b, eps) should equal WithinEpsilon(b, a, eps)
    float a = 5.0f;
    float b = 5.1f;
    float epsilon = 0.2f;
    
    Assert.Equal(
        MathUtil.WithinEpsilon(a, b, epsilon),
        MathUtil.WithinEpsilon(b, a, epsilon)
    );
}

[Theory]
[InlineData(0.0f, 0.0f, 0.0f, true)]
[InlineData(1.0f, 1.0f, 0.0f, true)] // Zero epsilon should only match exact values
[InlineData(1.0f, 1.0001f, 0.0f, false)]
public void TestWithinEpsilonZeroEpsilon(float a, float b, float epsilon, bool expected)
{
    Assert.Equal(expected, MathUtil.WithinEpsilon(a, b, epsilon));
}
```

### 8. **Lerp Formula Verification**

**Current Issue:** Tests verify results but not the actual formula implementation.

**Potential Surviving Mutants:**
- Changing `(1 - amount)` to `(1 + amount)` or similar
- Swapping `from` and `to` parameters
- Modifying the formula structure

**Recommended Additional Tests:**

```csharp
[Fact]
public void TestLerpFloatFormula()
{
    // Verify the exact formula: ((1 - amount) * from) + (amount * to)
    float from = 10.0f;
    float to = 20.0f;
    float amount = 0.3f;
    
    float expected = ((1 - amount) * from) + (amount * to);
    Assert.Equal(expected, MathUtil.Lerp(from, to, amount), 5);
}

[Theory]
[InlineData(0, 100, -0.5f, -50)]  // amount < 0 (extrapolation)
[InlineData(0, 100, 1.5f, 150)]   // amount > 1 (extrapolation)
public void TestLerpExtrapolation(float from, float to, float amount, float expected)
{
    Assert.Equal(expected, MathUtil.Lerp(from, to, amount), 5);
}

[Fact]
public void TestLerpSwappedParameters()
{
    // Lerp(a, b, t) should not equal Lerp(b, a, t) unless t = 0.5
    float result1 = MathUtil.Lerp(10.0f, 20.0f, 0.3f);
    float result2 = MathUtil.Lerp(20.0f, 10.0f, 0.3f);
    Assert.NotEqual(result1, result2);
    
    // But they should be symmetric around the midpoint
    Assert.Equal(15.0f, (result1 + result2) / 2.0f, 5);
}
```

### 9. **Conversion Functions Accuracy**

**Current Issue:** Conversion functions need more precise validation.

**Potential Surviving Mutants:**
- Changing division to multiplication or vice versa
- Modifying conversion constants

**Recommended Additional Tests:**

```csharp
[Fact]
public void TestAngleConversionsRoundTrip()
{
    // Test that conversions are reversible
    float degrees = 45.0f;
    
    // Degrees -> Radians -> Degrees
    var radians = MathUtil.DegreesToRadians(degrees);
    var backToDegrees = MathUtil.RadiansToDegrees(radians);
    Assert.Equal(degrees, backToDegrees, 5);
    
    // Degrees -> Revolutions -> Degrees
    var revolutions = MathUtil.DegreesToRevolutions(degrees);
    var backToDegrees2 = MathUtil.RevolutionsToDegrees(revolutions);
    Assert.Equal(degrees, backToDegrees2, 5);
    
    // Degrees -> Radians -> Gradians -> Radians -> Degrees
    var gradians = MathUtil.RadiansToGradians(radians);
    var backToRadians = MathUtil.GradiansToRadians(gradians);
    var backToDegrees3 = MathUtil.RadiansToDegrees(backToRadians);
    Assert.Equal(degrees, backToDegrees3, 5);
}

[Theory]
[InlineData(360.0f, 0.0f)] // 360 degrees = 0 degrees (mod 360)
[InlineData(720.0f, 0.0f)] // 720 degrees = 0 degrees
public void TestDegreesToRadiansFullRotations(float degrees, float expectedRadians)
{
    var radians = MathUtil.DegreesToRadians(degrees);
    // Allow for floating point precision in full rotations
    var normalizedRadians = radians % MathUtil.TwoPi;
    Assert.Equal(expectedRadians, normalizedRadians, 5);
}
```

### 10. **IsZero and IsOne Tolerance Testing**

**Current Issue:** Tests should verify the exact tolerance boundaries.

**Potential Surviving Mutants:**
- Changing `<` to `<=`
- Modifying the ZeroTolerance constant usage

**Recommended Additional Tests:**

```csharp
[Theory]
[InlineData(MathUtil.ZeroTolerance, false)]  // Exactly at tolerance (should be false with <)
[InlineData(MathUtil.ZeroTolerance * 0.99999f, true)]  // Just inside tolerance
[InlineData(-MathUtil.ZeroTolerance, false)]  // Exactly at negative tolerance
[InlineData(-MathUtil.ZeroTolerance * 0.99999f, true)]  // Just inside negative tolerance
public void TestIsZeroExactBoundaries(float value, bool expected)
{
    Assert.Equal(expected, MathUtil.IsZero(value));
}

[Theory]
[InlineData(1.0f + MathUtil.ZeroTolerance, false)]  // Exactly at tolerance above 1
[InlineData(1.0f - MathUtil.ZeroTolerance, false)]  // Exactly at tolerance below 1
[InlineData(1.0f + MathUtil.ZeroTolerance * 0.99999f, true)]
[InlineData(1.0f - MathUtil.ZeroTolerance * 0.99999f, true)]
public void TestIsOneExactBoundaries(float value, bool expected)
{
    Assert.Equal(expected, MathUtil.IsOne(value));
}

[Fact]
public void TestIsZeroDouble()
{
    Assert.True(MathUtil.IsZero(0.0));
    Assert.True(MathUtil.IsZero(MathUtil.ZeroToleranceDouble * 0.5));
    Assert.False(MathUtil.IsZero(MathUtil.ZeroToleranceDouble * 2.0));
}
```

## Implementation Priority

1. **High Priority** (Critical for mutation score):
   - InverseLerp division by zero handling
   - Clamp boundary conditions
   - NearEqual ULP precision testing

2. **Medium Priority** (Important for robustness):
   - SmoothStep/SmootherStep boundary and formula tests
   - WithinEpsilon symmetry tests
   - Lerp formula verification

3. **Low Priority** (Nice to have):
   - Round-trip conversion tests
   - IsPow2 comprehensive edge cases
   - IsInRange inverted range behavior

## Mutation Testing Best Practices Applied

1. **Boundary Value Testing**: Added tests at exact boundaries of conditions
2. **Off-by-One Testing**: Verified `<` vs `<=` operators
3. **Formula Verification**: Tests that verify the exact mathematical formula
4. **Symmetry Testing**: Ensure commutative operations behave correctly
5. **Special Value Testing**: NaN, Infinity, -Infinity, 0, -0
6. **Round-Trip Testing**: Verify inverse operations return to original value
7. **Edge Case Coverage**: Extreme values, negative numbers, zero divisions

## Next Steps

1. Implement the recommended tests in respective test files
2. Run the test suite to ensure all new tests pass
3. Attempt to run Stryker again with a simplified project structure or use an alternative mutation testing approach
4. Monitor mutation score improvements
5. Iterate on areas with surviving mutants

## Conclusion

While Stryker.NET encountered technical challenges with the Stride build system, manual analysis using mutation testing principles has revealed numerous opportunities to strengthen the test suite. Implementing these improvements will significantly increase confidence in the correctness of the mathematics library and catch potential bugs that could arise from future code changes.
