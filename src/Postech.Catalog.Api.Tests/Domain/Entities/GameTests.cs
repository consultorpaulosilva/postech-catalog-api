using FluentAssertions;
using Postech.Catalog.Api.Domain.Entities;

namespace Postech.Catalog.Api.Tests.Domain.Entities;

public class GameTests
{
    [Fact]
    public void Constructor_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        var releaseDate = new DateTime(2025, 6, 15);

        // Act
        var game = new Game("Test Game", "A fun game", 49.99m, "RPG", releaseDate);

        // Assert
        game.Id.Should().NotBeEmpty();
        game.Name.Should().Be("Test Game");
        game.Description.Should().Be("A fun game");
        game.Price.Should().Be(49.99m);
        game.Genre.Should().Be("RPG");
        game.ReleaseDate.Should().Be(releaseDate);
        game.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        // Act
        var game1 = new Game("Game 1", "Desc", 10m, "FPS", DateTime.UtcNow);
        var game2 = new Game("Game 2", "Desc", 20m, "RPG", DateTime.UtcNow);

        // Assert
        game1.Id.Should().NotBe(game2.Id);
    }

    [Fact]
    public void UpdateName_ShouldChangeName()
    {
        // Arrange
        var game = new Game("Old Name", "Desc", 10m, "RPG", DateTime.UtcNow);

        // Act
        game.UpdateName("New Name");

        // Assert
        game.Name.Should().Be("New Name");
    }

    [Fact]
    public void UpdateDescription_ShouldChangeDescription()
    {
        // Arrange
        var game = new Game("Name", "Old Desc", 10m, "RPG", DateTime.UtcNow);

        // Act
        game.UpdateDescription("New Desc");

        // Assert
        game.Description.Should().Be("New Desc");
    }

    [Fact]
    public void UpdatePrice_ShouldChangePrice()
    {
        // Arrange
        var game = new Game("Name", "Desc", 10m, "RPG", DateTime.UtcNow);

        // Act
        game.UpdatePrice(25.50m);

        // Assert
        game.Price.Should().Be(25.50m);
    }

    [Fact]
    public void UpdateGenre_ShouldChangeGenre()
    {
        // Arrange
        var game = new Game("Name", "Desc", 10m, "RPG", DateTime.UtcNow);

        // Act
        game.UpdateGenre("FPS");

        // Assert
        game.Genre.Should().Be("FPS");
    }

    [Fact]
    public void UpdateReleaseDate_ShouldChangeReleaseDate()
    {
        // Arrange
        var game = new Game("Name", "Desc", 10m, "RPG", new DateTime(2025, 1, 1));
        var newDate = new DateTime(2025, 12, 25);

        // Act
        game.UpdateReleaseDate(newDate);

        // Assert
        game.ReleaseDate.Should().Be(newDate);
    }
}
