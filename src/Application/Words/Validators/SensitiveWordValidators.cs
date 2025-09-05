using FluentValidation;
using FlashAssessment.Application.Words.Dto;

namespace FlashAssessment.Application.Words.Validators;

public sealed class CreateSensitiveWordRequestValidator : AbstractValidator<CreateSensitiveWordRequestDto>
{
    public CreateSensitiveWordRequestValidator()
    {
        RuleFor(x => x.Word)
            .NotEmpty()
            .MaximumLength(128);

        // Basic safety: disallow characters that would produce pathological regex if ever used
        // in future patterns. Since we escape words for regex, this is conservative hardening.
        RuleFor(x => x.Word)
            .Matches("^[^\\\\()\\[\\]{}|?+]+$")
            .WithMessage("Word contains potentially unsafe pattern characters.");

        RuleFor(x => x.Category)
            .MaximumLength(64)
            .When(x => x.Category is not null);

        RuleFor(x => x.Severity)
            .InclusiveBetween((byte)0, (byte)5)
            .When(x => x.Severity.HasValue);
    }
}

public sealed class UpdateSensitiveWordRequestValidator : AbstractValidator<UpdateSensitiveWordRequestDto>
{
    public UpdateSensitiveWordRequestValidator()
    {
        RuleFor(x => x.Word)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Word)
            .Matches("^[^\\\\()\\[\\]{}|?+]+$")
            .WithMessage("Word contains potentially unsafe pattern characters.");

        RuleFor(x => x.Category)
            .MaximumLength(64)
            .When(x => x.Category is not null);

        RuleFor(x => x.Severity)
            .InclusiveBetween((byte)0, (byte)5)
            .When(x => x.Severity.HasValue);

        RuleFor(x => x.RowVersion)
            .NotNull()
            .Must(rv => rv.Length > 0);
    }
}

public sealed class ListWordsQueryValidator : AbstractValidator<ListWordsQueryDto>
{
    public ListWordsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Search).MaximumLength(128).When(x => x.Search is not null);
    }
}


