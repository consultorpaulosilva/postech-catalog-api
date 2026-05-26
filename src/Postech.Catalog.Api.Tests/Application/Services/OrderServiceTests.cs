using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Postech.Catalog.Api.Application.Services;
using Postech.Catalog.Api.Domain.Entities;
using Postech.Catalog.Api.Domain.Enums;
using Postech.Catalog.Api.Domain.Errors;
using Postech.Catalog.Api.Infrastructure.Messaging;
using Postech.Catalog.Api.Infrastructure.Repositories;
using Postech.Shared.Contracts.Events;

namespace Postech.Catalog.Api.Tests.Application.Services;

public class OrderServiceTests
{
    private readonly ILogger<OrderService> _logger = Substitute.For<ILogger<OrderService>>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly IGameRepository _gameRepository = Substitute.For<IGameRepository>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();

    private OrderService GetOrderService() => new OrderService(
        _logger, _eventPublisher, _gameRepository, _orderRepository);

    [Fact]
    public async Task PlaceOrder_WithExistingGame_ShouldCreateOrderAndPublishEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game = new Game("Test Game", "Desc", 59.99m, "RPG", new DateTime(2025, 1, 1));
        _gameRepository.GetByIdAsync(game.Id, Arg.Any<CancellationToken>()).Returns(game);

        var service = GetOrderService();

        // Act
        var result = await service.PlaceOrder(userId, game.Id, "test@example.com", "Test User");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
        await _orderRepository.Received(1).AddAsync(Arg.Is<Order>(o =>
            o.UserId == userId &&
            o.GameId == game.Id &&
            o.TotalAmount == 59.99m &&
            o.Status == OrderStatus.Placed), Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(Arg.Is<OrderPlacedEvent>(e =>
            e.UserId == userId &&
            e.GameId == game.Id &&
            e.TotalAmount == 59.99m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceOrder_WithNonExistingGame_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _gameRepository.GetByIdAsync(gameId, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var service = GetOrderService();

        // Act
        var result = await service.PlaceOrder(userId, gameId, "test@example.com", "Test User");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Game.NotFound.Code);
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _eventPublisher.DidNotReceive().PublishAsync(Arg.Any<OrderPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUserLibraryAsync_WithCompletedOrders_ShouldReturnGames()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var game1 = new Game("Game A", "Desc A", 29.99m, "RPG", new DateTime(2025, 1, 1));
        var game2 = new Game("Game B", "Desc B", 49.99m, "FPS", new DateTime(2025, 6, 1));

        var orders = new List<Order>
        {
            new Order { OrderId = Guid.NewGuid(), UserId = userId, GameId = game1.Id, TotalAmount = 29.99m, Status = OrderStatus.Completed },
            new Order { OrderId = Guid.NewGuid(), UserId = userId, GameId = game2.Id, TotalAmount = 49.99m, Status = OrderStatus.Completed }
        };

        _orderRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(orders);
        _gameRepository.GetByIdAsync(game1.Id, Arg.Any<CancellationToken>()).Returns(game1);
        _gameRepository.GetByIdAsync(game2.Id, Arg.Any<CancellationToken>()).Returns(game2);

        var service = GetOrderService();

        // Act
        var result = await service.GetUserLibraryAsync(userId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("Game A");
        result.Value[1].Name.Should().Be("Game B");
    }

    [Fact]
    public async Task GetUserLibraryAsync_WithNoOrders_ShouldReturnEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _orderRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<Order>());

        var service = GetOrderService();

        // Act
        var result = await service.GetUserLibraryAsync(userId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserLibraryAsync_WhenGameDeleted_ShouldSkipMissingGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingGame = new Game("Game A", "Desc", 29.99m, "RPG", new DateTime(2025, 1, 1));
        var deletedGameId = Guid.NewGuid();

        var orders = new List<Order>
        {
            new Order { OrderId = Guid.NewGuid(), UserId = userId, GameId = existingGame.Id, TotalAmount = 29.99m, Status = OrderStatus.Completed },
            new Order { OrderId = Guid.NewGuid(), UserId = userId, GameId = deletedGameId, TotalAmount = 19.99m, Status = OrderStatus.Completed }
        };

        _orderRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(orders);
        _gameRepository.GetByIdAsync(existingGame.Id, Arg.Any<CancellationToken>()).Returns(existingGame);
        _gameRepository.GetByIdAsync(deletedGameId, Arg.Any<CancellationToken>()).Returns((Game?)null);

        var service = GetOrderService();

        // Act
        var result = await service.GetUserLibraryAsync(userId);

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("Game A");
    }
}
