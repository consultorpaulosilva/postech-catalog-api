using ErrorOr;

namespace Postech.Catalog.Api.Domain.Errors;

public static partial class Errors
{
    public static class Game
    {
        public static Error NameRequired => Error.Validation(
            code: "Game.Name.Required",
            description: "Name is required.");

        public static Error NotFound => Error.NotFound(
            code: "Game.NotFound",
            description: "Game not found.");
    }

    public static class Review
    {
        public static Error RatingOutOfRange => Error.Validation(
            code: "Review.Rating.OutOfRange",
            description: "Rating must be between 1 and 5.");

        public static Error CommentRequired => Error.Validation(
            code: "Review.Comment.Required",
            description: "Comment is required.");

        public static Error AlreadyReviewed => Error.Conflict(
            code: "Review.AlreadyReviewed",
            description: "User has already reviewed this game.");

        public static Error NotFound => Error.NotFound(
            code: "Review.NotFound",
            description: "Review not found.");

        public static Error NotOwner => Error.Forbidden(
            code: "Review.NotOwner",
            description: "Only the review author can delete this review.");
    }
}

