using System.Security.Claims;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Application.Services;
using Postech.Catalog.Api.Application.Validations;
using Postech.Catalog.Api.Domain.Authorization;

namespace Postech.Catalog.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/review");

        group.MapPost("", async (ClaimsPrincipal user, [FromBody] CreateGameReviewRequest request,
                [FromServices] IGameReviewService reviewService, CancellationToken ct) =>
                await CreateReviewAsync(user, request, reviewService, ct))
            .WithName("CreateReview")
            .WithSummary("Create a review for a game")
            .RequireAuthorization(Policies.RequireUserRole)
            .Produces<GameReviewResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapGet("/game/{gameId:guid}", async (Guid gameId,
                [FromServices] IGameReviewService reviewService, CancellationToken ct) =>
                await GetReviewsByGameAsync(gameId, reviewService, ct))
            .WithName("GetReviewsByGame")
            .WithSummary("Get all reviews for a game")
            .Produces<List<GameReviewResponse>>(StatusCodes.Status200OK);

        group.MapGet("/game/{gameId:guid}/summary", async (Guid gameId,
                [FromServices] IGameReviewService reviewService, CancellationToken ct) =>
                await GetReviewSummaryAsync(gameId, reviewService, ct))
            .WithName("GetReviewSummary")
            .WithSummary("Get average rating and total reviews for a game")
            .Produces<GameReviewSummaryResponse>(StatusCodes.Status200OK);

        group.MapDelete("/{reviewId}", async (string reviewId, ClaimsPrincipal user,
                [FromServices] IGameReviewService reviewService, CancellationToken ct) =>
                await DeleteReviewAsync(reviewId, user, reviewService, ct))
            .WithName("DeleteReview")
            .WithSummary("Delete a review")
            .RequireAuthorization(Policies.RequireUserRole)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateReviewAsync(ClaimsPrincipal user, CreateGameReviewRequest request,
        IGameReviewService reviewService, CancellationToken ct)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var validation = GameReviewRequestValidator.Validate(request);
        if (validation.IsError)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = string.Join(";\n", validation.Errors.Select(e => e.Description))
            });
        }

        var result = await reviewService.CreateReviewAsync(userId, request, ct);
        if (result.IsError)
            return ToErrorResult(result.Errors);

        return Results.Created($"/api/review/{result.Value.Id}", result.Value);
    }

    private static async Task<IResult> GetReviewsByGameAsync(Guid gameId, IGameReviewService reviewService, CancellationToken ct)
    {
        var result = await reviewService.GetReviewsByGameIdAsync(gameId, ct);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> GetReviewSummaryAsync(Guid gameId, IGameReviewService reviewService, CancellationToken ct)
    {
        var result = await reviewService.GetReviewSummaryAsync(gameId, ct);
        return Results.Ok(result.Value);
    }

    private static async Task<IResult> DeleteReviewAsync(string reviewId, ClaimsPrincipal user,
        IGameReviewService reviewService, CancellationToken ct)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Results.Unauthorized();

        var result = await reviewService.DeleteReviewAsync(reviewId, userId, ct);
        if (result.IsError)
            return ToErrorResult(result.Errors);

        return Results.NoContent();
    }

    private static IResult ToErrorResult(List<Error> errors)
    {
        var first = errors[0];
        return first.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = string.Join(";\n", errors.Select(e => e.Description))
            }),
            ErrorType.Validation => Results.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = string.Join(";\n", errors.Select(e => e.Description))
            }),
            ErrorType.Conflict => Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = string.Join(";\n", errors.Select(e => e.Description))
            }),
            ErrorType.Forbidden => Results.Forbid(),
            _ => Results.Problem(
                detail: string.Join(";\n", errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
