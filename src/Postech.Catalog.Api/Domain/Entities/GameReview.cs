using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Postech.Catalog.Api.Domain.Entities;

public class GameReview
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    private GameReview() { }

    public GameReview(Guid gameId, Guid userId, int rating, string comment)
    {
        GameId = gameId;
        UserId = userId;
        Rating = rating;
        Comment = comment;
        CreatedAt = DateTime.UtcNow;
    }
}
