using FluentValidation;
using ShortnerUrl.Dtos;
using ShortnerUrl.Services;

namespace ShortnerUrl.Validators
{
    public class CreateShortUrlRequestValidator : AbstractValidator<CreateShortUrlRequest>
    {
        public CreateShortUrlRequestValidator()
        {
            RuleFor(x => x.Url)
                .NotEmpty().WithMessage("URL is required.")
                .MaximumLength(2048).WithMessage("URL must not exceed 2048 characters.")
                .Must(url =>
                {
                    var (isValid, _) = UrlValidator.ValidateUrl(url);
                    return isValid;
                }).WithMessage(x =>
                {
                    var (_, error) = UrlValidator.ValidateUrl(x.Url);
                    return error ?? "URL must be a valid absolute http or https URL.";
                });

            RuleFor(x => x.CustomAlias)
                .Must(alias =>
                {
                    if (string.IsNullOrWhiteSpace(alias)) return true;
                    var (isValid, _) = UrlValidator.ValidateCustomAlias(alias);
                    return isValid;
                }).WithMessage(x =>
                {
                    var (_, error) = UrlValidator.ValidateCustomAlias(x.CustomAlias);
                    return error ?? "Custom alias must be 3-30 alphanumeric characters or hyphens, and not a reserved word.";
                });

            RuleFor(x => x.ExpiresAt)
                .GreaterThan(DateTime.UtcNow)
                .When(x => x.ExpiresAt.HasValue)
                .WithMessage("Expiration date must be in the future.");
        }
    }
}
