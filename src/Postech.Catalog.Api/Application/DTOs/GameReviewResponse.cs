namespace Postech.Catalog.Api.Application.DTOs;

public record GameReviewResponse(
    string Id,
    Guid GameId,
    Guid UserId,
    int Rating,
    string Comment,
    DateTime CreatedAt);

public record GameReviewSummaryResponse(
    Guid GameId,
    double AverageRating,
    int TotalReviews);
