using ErrorOr;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Domain.Entities;
using Postech.Catalog.Api.Domain.Errors;
using Postech.Catalog.Api.Infrastructure.Repositories;

namespace Postech.Catalog.Api.Application.Services;

public class GameReviewService : IGameReviewService
{
    private readonly IGameReviewRepository _reviewRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<GameReviewService> _logger;

    public GameReviewService(
        IGameReviewRepository reviewRepository,
        IGameRepository gameRepository,
        ILogger<GameReviewService> logger)
    {
        _reviewRepository = reviewRepository;
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public async Task<ErrorOr<GameReviewResponse>> CreateReviewAsync(Guid userId, CreateGameReviewRequest request, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken);
        if (game is null)
            return Errors.Game.NotFound;

        var existing = await _reviewRepository.GetByUserAndGameAsync(userId, request.GameId, cancellationToken);
        if (existing is not null)
            return Errors.Review.AlreadyReviewed;

        var review = new GameReview(request.GameId, userId, request.Rating, request.Comment);
        await _reviewRepository.AddAsync(review, cancellationToken);

        _logger.LogInformation("Review created for game {GameId} by user {UserId}", request.GameId, userId);

        return new GameReviewResponse(review.Id, review.GameId, review.UserId, review.Rating, review.Comment, review.CreatedAt);
    }

    public async Task<ErrorOr<List<GameReviewResponse>>> GetReviewsByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var reviews = await _reviewRepository.GetByGameIdAsync(gameId, cancellationToken);

        var response = reviews.Select(r =>
            new GameReviewResponse(r.Id, r.GameId, r.UserId, r.Rating, r.Comment, r.CreatedAt))
            .ToList();

        return response;
    }

    public async Task<ErrorOr<GameReviewSummaryResponse>> GetReviewSummaryAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var (averageRating, totalReviews) = await _reviewRepository.GetSummaryByGameIdAsync(gameId, cancellationToken);
        return new GameReviewSummaryResponse(gameId, averageRating, totalReviews);
    }

    public async Task<ErrorOr<Success>> DeleteReviewAsync(string reviewId, Guid userId, CancellationToken cancellationToken = default)
    {
        var review = await _reviewRepository.GetByIdAsync(reviewId, cancellationToken);
        if (review is null)
            return Errors.Review.NotFound;

        if (review.UserId != userId)
            return Errors.Review.NotOwner;

        await _reviewRepository.DeleteAsync(reviewId, cancellationToken);
        _logger.LogInformation("Review {ReviewId} deleted by user {UserId}", reviewId, userId);

        return Result.Success;
    }
}
