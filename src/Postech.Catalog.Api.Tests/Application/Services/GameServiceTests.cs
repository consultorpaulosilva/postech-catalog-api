using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Application.Services;
using Postech.Catalog.Api.Domain.Entities;
using Postech.Catalog.Api.Domain.Errors;
using Postech.Catalog.Api.Infrastructure.Messaging;
using Postech.Catalog.Api.Infrastructure.Repositories;
using Postech.Catalog.Api.Infrastructure.Search;

namespace Postech.Catalog.Api.Tests.Application.Services;

public class GameServiceTests
{
    private readonly IGameRepository _gameRepository = Substitute.For<IGameRepository>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly IGameSearchIndex _searchIndex = Substitute.For<IGameSearchIndex>();
    private readonly IDistributedCache _cache;
    private readonly ILogger<GameService> _logger = Substitute.For<ILogger<GameService>>();

    public GameServiceTests()
    {
        _cache = Substitute.For<IDistributedCache>();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
    }

    private GameService GetGameService() =>
        new GameService(_gameRepository, _eventPublisher, _cache, _searchIndex, _logger);

    [Fact]
    public async Task GetAllGamesAsync_ShouldReturnAllGames()
    {
        // Arrange
        var games = new List<Game?>
        {
            new Game("Game A", "Desc A", 29.99m, "RPG", new DateTime(2025, 1, 1)),
            new Game("Game B", "Desc B", 49.99m, "FPS", new DateTime(2025, 6, 1))
        };
        _gameRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(games);

        var service = GetGameService();

        // Act
        var result = await service.GetAllGamesAsync();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("Game A");
        result.Value[1].Name.Should().Be("Game B");
    }

    [Fact]
    public async Task GetAllGamesAsync_WhenEmpty_ShouldReturnEmptyList()
    {
        // Arrange
        _gameRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Game?>());

        var service = GetGameService();

        // Act
        var result = await service.GetAllGamesAsync();

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGameByIdAsync_WithExistingId_ShouldReturnGame()
    {
        // Arrange
        var game = new Game("Test Game", "A test", 19.99m, "Puzzle", new DateTime(2025, 3, 15));
        _gameRepository.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var service = GetGameService();

        // Act
        var result = await service.GetGameByIdAsync(game.Id);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(game.Id);
        result.Value.Name.Should().Be("Test Game");
        result.Value.Price.Should().Be(19.99m);
    }

    [Fact]
    public async Task GetGameByIdAsync_WithNonExistingId_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _gameRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var service = GetGameService();

        // Act
        var result = await service.GetGameByIdAsync(id);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Game.NotFound.Code);
    }

    [Fact]
    public async Task CreateGameAsync_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var request = new CreateGameRequest("New Game", "Great game", 39.99m, "Adventure", new DateTime(2025, 12, 1));
        var service = GetGameService();

        // Act
        var result = await service.CreateGameAsync(request);

        // Assert
        result.IsError.Should().BeFalse();
        await _gameRepository.Received(1).AddAsync(Arg.Is<Game>(g =>
            g.Name == "New Game" &&
            g.Price == 39.99m &&
            g.Genre == "Adventure"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateGameAsync_WithExistingGame_ShouldUpdateFields()
    {
        // Arrange
        var game = new Game("Old Name", "Old Desc", 29.99m, "RPG", new DateTime(2025, 1, 1));
        _gameRepository.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var request = new UpdateGameRequest(game.Id, Name: "New Name", Price: 19.99m);
        var service = GetGameService();

        // Act
        var result = await service.UpdateGameAsync(request);

        // Assert
        result.IsError.Should().BeFalse();
        game.Name.Should().Be("New Name");
        game.Price.Should().Be(19.99m);
        game.Description.Should().Be("Old Desc");
        await _gameRepository.Received(1).UpdateAsync(game, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateGameAsync_WithNoChanges_ShouldNotCallRepository()
    {
        // Arrange
        var game = new Game("Name", "Desc", 29.99m, "RPG", new DateTime(2025, 1, 1));
        _gameRepository.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var request = new UpdateGameRequest(game.Id);
        var service = GetGameService();

        // Act
        var result = await service.UpdateGameAsync(request);

        // Assert
        result.IsError.Should().BeFalse();
        await _gameRepository.DidNotReceive().UpdateAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateGameAsync_WithNonExistingGame_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _gameRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var request = new UpdateGameRequest(id, Name: "Updated");
        var service = GetGameService();

        // Act
        var result = await service.UpdateGameAsync(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Game.NotFound.Code);
    }

    [Fact]
    public async Task DeleteGameAsync_WithExistingGame_ShouldSucceed()
    {
        // Arrange
        var game = new Game("To Delete", "Desc", 9.99m, "Indie", new DateTime(2025, 1, 1));
        _gameRepository.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var service = GetGameService();

        // Act
        var result = await service.DeleteGameAsync(game.Id);

        // Assert
        result.IsError.Should().BeFalse();
        await _gameRepository.Received(1).DeleteAsync(game.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGameAsync_WithNonExistingGame_ShouldReturnNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _gameRepository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var service = GetGameService();

        // Act
        var result = await service.DeleteGameAsync(id);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Game.NotFound.Code);
        await _gameRepository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
