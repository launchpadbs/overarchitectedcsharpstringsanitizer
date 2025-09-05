// Validator tests ensure DTO constraints are enforced at the Application layer
// so controllers fail fast with 400 for invalid requests.
using System.Threading.Tasks;
using FlashAssessment.Application.Words.Dto;
using FlashAssessment.Application.Words.Validators;
using FluentAssertions;
using Xunit;

public class ValidatorsTests
{
    // Word length must be <= 128; this verifies boundary enforcement
    [Fact]
    public async Task CreateWord_Invalid_TooLong_ShouldFail()
    {
        var v = new CreateSensitiveWordRequestValidator();
        var dto = new CreateSensitiveWordRequestDto { Word = new string('a', 129) };
        var result = await v.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
    }

    // Update requires a non-empty RowVersion for optimistic concurrency
    [Fact]
    public async Task UpdateWord_MissingRowVersion_ShouldFail()
    {
        var v = new UpdateSensitiveWordRequestValidator();
        var dto = new UpdateSensitiveWordRequestDto { Word = "ok", IsActive = true, Category = null, Severity = null, RowVersion = new byte[]{} };
        var result = await v.ValidateAsync(dto);
        result.IsValid.Should().BeFalse();
    }
}


