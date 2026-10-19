using RigKit.Infrastructure.Nvidia;

namespace RigKit.UnitTests;

public sealed class CsvTests
{
    [Fact]
    public void Split_HandlesQuotedCommas()
    {
        var fields = Csv.Split("\"NVIDIA CMP 90HX\",10240,616.92");

        Assert.Equal(3, fields.Length);
        Assert.Equal("NVIDIA CMP 90HX", fields[0]);
        Assert.Equal("10240", fields[1]);
    }

    [Theory]
    [InlineData("[N/A]")]
    [InlineData("N/A")]
    [InlineData("")]
    public void ToDouble_ReturnsNullForUnavailable(string value)
    {
        Assert.Null(Csv.ToDouble(value));
    }

    [Fact]
    public void ToDouble_ParsesDecimalPoint()
    {
        Assert.Equal(249.6, Csv.ToDouble("249.60"));
    }

    [Fact]
    public void ToInt_ParsesInvariant()
    {
        Assert.Equal(10240, Csv.ToInt("10240"));
    }
}
