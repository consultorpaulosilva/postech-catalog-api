using ErrorOr;
using Postech.Catalog.Api.Application.DTOs;

namespace Postech.Catalog.Api.Application.Services;

public interface IGameReviewService
{
    Task<ErrorOr<GameReviewResponse>> CreateReviewAsync(Guid userId, CreateGameReviewRequest request, CancellationToken cancellationToken = default);
    Task<ErrorOr<List<GameReviewResponse>>> GetReviewsByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<ErrorOr<GameReviewSummaryResponse>> GetReviewSummaryAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<ErrorOr<Success>> DeleteReviewAsync(string reviewId, Guid userId, CancellationToken cancellationToken = default);
}
