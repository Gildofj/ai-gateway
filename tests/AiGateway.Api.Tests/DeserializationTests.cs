using System.Text.Json;
using System.Text.Json.Serialization;
using AiGateway.Api.Core.Models;
using FluentAssertions;
using Xunit;

namespace AiGateway.Api.Tests;

public class DeserializationTests
{
    [Fact]
    public void ChatRequest_ShouldDeserializeStringEnums()
    {
        // Arrange
        var json = """
        {
            "prompt": "Olá! Você está funcionando corretamente no ambiente de produção?",
            "domain": "Conversation",
            "complexity": "Low"
        }
        """;

        // Minimal API uses JsonSerializerOptions.Web defaults
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        // Act
        var request = JsonSerializer.Deserialize<ChatRequest>(json, options);

        // Assert
        request.Should().NotBeNull();
        request!.Prompt.Should().Be("Olá! Você está funcionando corretamente no ambiente de produção?");
        request.Domain.Should().Be(TaskDomain.Conversation);
        request.Complexity.Should().Be(ModelComplexity.Low);
    }

    [Fact]
    public void ChatRequest_ShouldFailWithoutConverter()
    {
        // Arrange
        var json = """
        {
            "prompt": "Test",
            "domain": "Conversation"
        }
        """;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Act
        Action act = () => JsonSerializer.Deserialize<ChatRequest>(json, options);

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ChatRequest_ShouldBeCaseInsensitiveForEnums()
    {
        // Arrange
        var json = """
        {
            "prompt": "Test",
            "domain": "conversation"
        }
        """;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        // Act
        var request = JsonSerializer.Deserialize<ChatRequest>(json, options);

        // Assert
        request.Should().NotBeNull();
        request!.Domain.Should().Be(TaskDomain.Conversation);
    }
}
