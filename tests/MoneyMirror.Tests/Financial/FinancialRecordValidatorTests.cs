using MoneyMirror.Financial;

namespace MoneyMirror.Tests.Financial;

public class FinancialRecordValidatorTests
{
    [Fact]
    public void Validate_ValidRecord_ReturnsNoErrors()
    {
        var errors = FinancialRecordValidator.Validate(
            "Cash",
            "Checking account",
            250m,
            new DateTime(2026, 9, 19));

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingRequiredFields_ReturnsFieldErrors()
    {
        var errors = FinancialRecordValidator.Validate(null, "", 0m, null);

        Assert.Contains("Choose a type.", errors);
        Assert.Contains("Enter a name.", errors);
        Assert.Contains("Choose an as-of date.", errors);
    }

    [Fact]
    public void Validate_NegativeAmount_ReturnsAmountError()
    {
        var errors = FinancialRecordValidator.Validate(
            "Loan",
            "Student loan",
            -1m,
            new DateTime(2026, 9, 19));

        Assert.Contains("Amount must be zero or more.", errors);
    }
}
