using StudyRoom.Infrastructure.Configuration;
using Xunit;

namespace StudyRoom.Infrastructure.Tests;

public class AppConfigurationTests : IDisposable
{
    private readonly string _tempConfigFile;

    public AppConfigurationTests()
    {
        _tempConfigFile = Path.Combine(Path.GetTempPath(), $"appsettings_test_{Guid.NewGuid():N}.json");
        ClearTestEnvVars();
    }

    public void Dispose()
    {
        ClearTestEnvVars();
        if (File.Exists(_tempConfigFile))
        {
            try { File.Delete(_tempConfigFile); } catch { }
        }
    }

    private static void ClearTestEnvVars()
    {
        Environment.SetEnvironmentVariable("STUDYROOM_COGNITO_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("STUDYROOM_COGNITO_REGION", null);
        Environment.SetEnvironmentVariable("STUDYROOM_WS_URL", null);
    }

    [Fact]
    public void Load_WhenNoConfigFileAndNoEnvVars_ReturnsDefaultValues()
    {
        // Act
        var config = AppConfiguration.Load(configFilePath: "non_existent_file.json");

        // Assert
        Assert.Equal(AppConfiguration.DefaultCognitoClientId, config.CognitoClientId);
        Assert.Equal(AppConfiguration.DefaultCognitoRegion, config.CognitoRegion);
        Assert.Equal(AppConfiguration.DefaultWebSocketEndpointUrl, config.WebSocketEndpointUrl);
    }

    [Fact]
    public void Load_WhenConfigFileExists_ParsesJsonProperly()
    {
        // Arrange
        var json = """
        {
          "Cognito": {
            "ClientId": "custom-client-id-123",
            "Region": "ap-southeast-1"
          },
          "WebSocket": {
            "EndpointUrl": "wss://ws.studyroom.io/prod"
          }
        }
        """;
        File.WriteAllText(_tempConfigFile, json);

        // Act
        var config = AppConfiguration.Load(_tempConfigFile);

        // Assert
        Assert.Equal("custom-client-id-123", config.CognitoClientId);
        Assert.Equal("ap-southeast-1", config.CognitoRegion);
        Assert.Equal("wss://ws.studyroom.io/prod", config.WebSocketEndpointUrl);
    }

    [Fact]
    public void Load_EnvironmentVariables_OverrideConfigFile()
    {
        // Arrange
        var json = """
        {
          "Cognito": {
            "ClientId": "file-client-id",
            "Region": "us-east-1"
          },
          "WebSocket": {
            "EndpointUrl": "wss://file-ws.io"
          }
        }
        """;
        File.WriteAllText(_tempConfigFile, json);

        Environment.SetEnvironmentVariable("STUDYROOM_COGNITO_CLIENT_ID", "env-client-id-999");
        Environment.SetEnvironmentVariable("STUDYROOM_COGNITO_REGION", "eu-central-1");
        Environment.SetEnvironmentVariable("STUDYROOM_WS_URL", "wss://env-ws.studyroom.io");

        // Act
        var config = AppConfiguration.Load(_tempConfigFile);

        // Assert
        Assert.Equal("env-client-id-999", config.CognitoClientId);
        Assert.Equal("eu-central-1", config.CognitoRegion);
        Assert.Equal("wss://env-ws.studyroom.io", config.WebSocketEndpointUrl);
    }

    [Fact]
    public void Load_WhenConfigFileIsCorrupted_FallsBackGracefully()
    {
        // Arrange
        File.WriteAllText(_tempConfigFile, "{ not valid json: true, ");

        // Act
        var config = AppConfiguration.Load(_tempConfigFile);

        // Assert
        Assert.Equal(AppConfiguration.DefaultCognitoClientId, config.CognitoClientId);
        Assert.Equal(AppConfiguration.DefaultCognitoRegion, config.CognitoRegion);
        Assert.Equal(AppConfiguration.DefaultWebSocketEndpointUrl, config.WebSocketEndpointUrl);
    }
}
