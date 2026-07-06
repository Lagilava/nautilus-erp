using ERP.Shared.Results;

namespace ERP.UnitTests.Shared;

public class ResultTests
{
    [Fact]
    public void Success_result_exposes_value_and_no_error()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_result_carries_error_and_throws_on_value_access()
    {
        var error = Error.Conflict("duplicate");
        var result = Result.Failure<int>(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Success_cannot_be_constructed_with_an_error()
        => Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
}
