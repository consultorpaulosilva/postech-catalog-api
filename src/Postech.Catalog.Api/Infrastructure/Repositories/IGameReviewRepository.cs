using Postech.Catalog.Api.Domain.Entities;

namespace Postech.Catalog.Api.Infrastructure.Repositories;

public interface IGameReviewRepository
{
    Task<IEnumerable<GameReview>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<GameReview?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<GameReview?> GetByUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task<(double AverageRating, int TotalReviews)> GetSummaryByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task AddAsync(GameReview review, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
