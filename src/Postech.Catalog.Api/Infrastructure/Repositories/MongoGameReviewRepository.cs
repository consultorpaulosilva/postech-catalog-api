using MongoDB.Driver;
using Postech.Catalog.Api.Domain.Entities;

namespace Postech.Catalog.Api.Infrastructure.Repositories;

public class MongoGameReviewRepository : IGameReviewRepository
{
    private readonly IMongoCollection<GameReview> _reviews;

    public MongoGameReviewRepository(IMongoDatabase database)
    {
        _reviews = database.GetCollection<GameReview>("game_reviews");
    }

    public async Task<IEnumerable<GameReview>> GetByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return await _reviews.Find(r => r.GameId == gameId)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<GameReview?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _reviews.Find(r => r.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GameReview?> GetByUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
    {
        return await _reviews.Find(r => r.UserId == userId && r.GameId == gameId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(double AverageRating, int TotalReviews)> GetSummaryByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var reviews = await _reviews.Find(r => r.GameId == gameId)
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
            return (0, 0);

        var average = reviews.Average(r => r.Rating);
        return (Math.Round(average, 1), reviews.Count);
    }

    public async Task AddAsync(GameReview review, CancellationToken cancellationToken = default)
    {
        await _reviews.InsertOneAsync(review, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _reviews.DeleteOneAsync(r => r.Id == id, cancellationToken);
    }
}
