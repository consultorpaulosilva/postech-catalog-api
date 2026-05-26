using ErrorOr;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Domain.Errors;

namespace Postech.Catalog.Api.Application.Validations;

public static class GameReviewRequestValidator
{
    public static ErrorOr<Success> Validate(CreateGameReviewRequest request)
    {
        var errors = new List<Error>();

        if (request.Rating < 1 || request.Rating > 5)
            errors.Add(Errors.Review.RatingOutOfRange);

        if (string.IsNullOrWhiteSpace(request.Comment))
            errors.Add(Errors.Review.CommentRequired);

        return errors.Count > 0
            ? errors
            : Result.Success;
    }
}
