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
                }).WithMessage("URL must be a valid absolute http or https URL and must not point to a private address.");

            RuleFor(x => x.CustomAlias)
                .Must(alias =>
                {
                    if (string.IsNullOrWhiteSpace(alias)) return true;
                    var (isValid, _) = UrlValidator.ValidateCustomAlias(alias);
                    return isValid;
                }).WithMessage("Custom alias must be 3-30 alphanumeric characters or hyphens, and not a reserved word.");
        }
    }
}
