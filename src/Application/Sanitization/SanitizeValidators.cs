using FluentValidation;

namespace FlashAssessment.Application.Sanitization;

public sealed class SanitizeRequestValidator : AbstractValidator<SanitizeRequestDto>
{
    public SanitizeRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotNull()
            .MaximumLength(100_000);

        RuleFor(x => x.Options!.MaskCharacter)
            .Must(s => s == null || s.Length == 1)
            .WithMessage("maskChar must be a single character if provided.")
            .When(x => x.Options is not null);

        RuleFor(x => x.Options!.FixedLength)
            .InclusiveBetween(1, 1024)
            .When(x => x.Options is not null && x.Options.Strategy == MaskStrategy.FixedLength);
    }
}


