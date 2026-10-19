using RigKit.Domain.Capabilities;

namespace RigKit.UnitTests;

public sealed class SupportMatrixTests
{
    [Theory]
    [InlineData("NVIDIA CMP 90HX", SupportLevel.Supported)]
    [InlineData("NVIDIA CMP 40HX", SupportLevel.Supported)]
    [InlineData("NVIDIA CMP 170HX", SupportLevel.Experimental)]
    [InlineData("NVIDIA P102-100", SupportLevel.Experimental)]
    [InlineData("NVIDIA P104-100", SupportLevel.Experimental)]
    [InlineData("NVIDIA P106-100", SupportLevel.Experimental)]
    [InlineData("NVIDIA GeForce RTX 3080", SupportLevel.DetectOnly)]
    [InlineData("", SupportLevel.Unsupported)]
    public void ForModel_ReturnsExpectedLevel(string model, SupportLevel expected)
    {
        Assert.Equal(expected, SupportMatrix.ForModel(model));
    }

    [Fact]
    public void AllowsGraphicsUnlock_IsFalseForPascalMiningCards()
    {
        Assert.False(SupportMatrix.AllowsGraphicsUnlock("NVIDIA P106-100"));
        Assert.True(SupportMatrix.AllowsGraphicsUnlock("NVIDIA CMP 90HX"));
    }
}
