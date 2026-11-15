using ColorRise.Saga.Expressions;
using Xunit;

namespace ColorRise.Saga.Tests.Expressions;

public sealed class SagaLiteralsTests
{
    [Theory]
    [InlineData(SagaTypeKind.Bool, "true")]
    [InlineData(SagaTypeKind.Int, "-12")]
    [InlineData(SagaTypeKind.Float, "0.12345")]
    [InlineData(SagaTypeKind.Float, "-2.5")]
    [InlineData(SagaTypeKind.Float, "0.000001")]
    [InlineData(SagaTypeKind.Float, "123456789.5")]
    [InlineData(SagaTypeKind.String, "\"say \\\"hi\\\" to C:\\\\mine\"")]
    [InlineData(SagaTypeKind.QuestState, "active")]
    [InlineData(SagaTypeKind.Act, "the_mine")]
    public void FormattedValueReadsBackTheSame(SagaTypeKind type, string literal)
    {
        Assert.Null(SagaLiterals.TryParse(literal, type, out var value));

        var formatted = SagaLiterals.Format(value);

        Assert.Null(SagaLiterals.TryParse(formatted, type, out var again));
        Assert.Equal(value, again);
    }

    [Fact]
    public void FormatWritesStringsWithEscapes() => Assert.Equal("\"a \\\"b\\\" \\\\c\"", SagaLiterals.Format(SagaValue.String("a \"b\" \\c")));
}
