using FluentAssertions;
using Postech.Catalog.Api.Application.DTOs;
using Postech.Catalog.Api.Application.Validations;

namespace Postech.Catalog.Api.Tests.Application.Validations;

public class RegisterGameRequestValidatorTests
{
    [Fact]
    public void Validate_WithValidRequest_ShouldPass()
    {
        // Arrange
        var request = new CreateGameRequest("Valid Game", "A great game", 29.99m, "RPG", new DateTime(2025, 6, 15));

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldReturnError()
    {
        // Arrange
        var request = new CreateGameRequest("", "Desc", 29.99m, "RPG", new DateTime(2025, 6, 15));

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Game.Name.Required");
    }

    [Fact]
    public void Validate_WithZeroPrice_ShouldReturnError()
    {
        // Arrange
        var request = new CreateGameRequest("Game", "Desc", 0m, "RPG", new DateTime(2025, 6, 15));

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Game.Price.Invalid");
    }

    [Fact]
    public void Validate_WithNegativePrice_ShouldReturnError()
    {
        // Arrange
        var request = new CreateGameRequest("Game", "Desc", -10m, "RPG", new DateTime(2025, 6, 15));

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Game.Price.Invalid");
    }

    [Fact]
    public void Validate_WithEmptyGenre_ShouldReturnError()
    {
        // Arrange
        var request = new CreateGameRequest("Game", "Desc", 29.99m, "", new DateTime(2025, 6, 15));

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Game.Genre.Required");
    }

    [Fact]
    public void Validate_WithDefaultReleaseDate_ShouldReturnError()
    {
        // Arrange
        var request = new CreateGameRequest("Game", "Desc", 29.99m, "RPG", default);

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Code == "Game.ReleaseDate.Invalid");
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var request = new CreateGameRequest("", "Desc", -5m, "", default);

        // Act
        var result = RegisterGameRequestValidator.Validate(request);

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(4);
    }
}
