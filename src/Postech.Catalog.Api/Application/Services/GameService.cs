using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Caching.Distributed;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Domain.Entities;
using Postech.Catalog.Api.Domain.Errors;
using Postech.Catalog.Api.Infrastructure.Messaging;
using Postech.Catalog.Api.Infrastructure.Repositories;

namespace Postech.Catalog.Api.Application.Services;

public class GameService : IGameService
{
    private const string AllGamesCacheKey = "games:all";
    private const string GameCacheKeyPrefix = "games:";
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(2)
    };

    private readonly IGameRepository _gameRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly IDistributedCache _cache;
    private readonly ILogger<GameService> _logger;

    public GameService(
        IGameRepository gameRepository,
        IEventPublisher eventPublisher,
        IDistributedCache cache,
        ILogger<GameService> logger)
    {
        _gameRepository = gameRepository;
        _eventPublisher = eventPublisher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ErrorOr<List<GameResponse>>> GetAllGamesAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(AllGamesCacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for all games");
            return JsonSerializer.Deserialize<List<GameResponse>>(cached)!;
        }

        var games = await _gameRepository.GetAllAsync(cancellationToken);

        var response = games.Select(game => new GameResponse(
            game!.Id,
            game.Name,
            game.Description,
            game.Price,
            game.Genre,
            game.ReleaseDate
        )).ToList();

        await _cache.SetStringAsync(AllGamesCacheKey, JsonSerializer.Serialize(response), CacheOptions, cancellationToken);

        return response;
    }

    public async Task<ErrorOr<GameResponse>> GetGameByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{GameCacheKeyPrefix}{id}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for game {GameId}", id);
            return JsonSerializer.Deserialize<GameResponse>(cached)!;
        }

        var game = await _gameRepository.GetByIdAsync(id, cancellationToken);

        if (game == null)
        {
            _logger.LogWarning("Game with ID {GameId} not found", id);
            return Errors.Game.NotFound;
        }

        var response = new GameResponse(
            game.Id,
            game.Name,
            game.Description,
            game.Price,
            game.Genre,
            game.ReleaseDate
        );

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response), CacheOptions, cancellationToken);

        return response;
    }

    public async Task<ErrorOr<Success>> CreateGameAsync(CreateGameRequest request, CancellationToken cancellationToken = default)
    {
        var game = new Game(
            request.Name,
            request.Description,
            request.Price,
            request.Genre,
            request.ReleaseDate
        );

        await _gameRepository.AddAsync(game, cancellationToken);
        await InvalidateListCacheAsync(cancellationToken);

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> UpdateGameAsync(UpdateGameRequest request, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(request.Id, cancellationToken);

        if (game == null)
        {
            _logger.LogWarning("Game with ID {GameId} not found for update", request.Id);
            return Errors.Game.NotFound;
        }

        bool changed = false;
        if (!string.IsNullOrEmpty(request.Name))
        {
            game.UpdateName(request.Name);
            changed = true;
        }

        if (!string.IsNullOrEmpty(request.Description))
        {
            game.UpdateDescription(request.Description);
            changed = true;
        }

        if (request.Price.HasValue)
        {
            game.UpdatePrice(request.Price.Value);
            changed = true;
        }

        if (!string.IsNullOrEmpty(request.Genre))
        {
            game.UpdateGenre(request.Genre);
            changed = true;
        }

        if (request.ReleaseDate.HasValue)
        {
            game.UpdateReleaseDate(request.ReleaseDate.Value);
            changed = true;
        }

        if (changed)
        {
            await _gameRepository.UpdateAsync(game, cancellationToken);
            await InvalidateGameCacheAsync(request.Id, cancellationToken);
        }

        return Result.Success;
    }

    public async Task<ErrorOr<Success>> DeleteGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(id, cancellationToken);

        if (game == null)
        {
            _logger.LogWarning("Game with ID {GameId} not found for deletion", id);
            return Errors.Game.NotFound;
        }

        await _gameRepository.DeleteAsync(id, cancellationToken);
        await InvalidateGameCacheAsync(id, cancellationToken);

        return Result.Success;
    }

    private async Task InvalidateGameCacheAsync(Guid gameId, CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync($"{GameCacheKeyPrefix}{gameId}", cancellationToken);
        await _cache.RemoveAsync(AllGamesCacheKey, cancellationToken);
    }

    private async Task InvalidateListCacheAsync(CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync(AllGamesCacheKey, cancellationToken);
    }
}