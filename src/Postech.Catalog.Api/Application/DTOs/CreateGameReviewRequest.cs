namespace Postech.Catalog.Api.Application.DTOs;

public record CreateGameReviewRequest(Guid GameId, int Rating, string Comment);
