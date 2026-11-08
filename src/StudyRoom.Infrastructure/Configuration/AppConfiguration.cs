using System.Text.Json;

namespace StudyRoom.Infrastructure.Configuration;

public sealed record AppConfiguration
{
    public const string DefaultCognitoClientId = "studyroom-desktop-client";
    public const string DefaultCognitoRegion = "us-east-1";
    public const string DefaultWebSocketEndpointUrl = "wss://127.0.0.1:8443/ws";

    public string CognitoClientId { get; init; } = DefaultCognitoClientId;
    public string CognitoRegion { get; init; } = DefaultCognitoRegion;
    public string WebSocketEndpointUrl { get; init; } = DefaultWebSocketEndpointUrl;

    public static AppConfiguration Load(string? configFilePath = null)
    {
        string clientId = DefaultCognitoClientId;
        string region = DefaultCognitoRegion;
        string wsUrl = DefaultWebSocketEndpointUrl;

        // 1. Try reading from configuration file
        string? targetPath = configFilePath;
        if (string.IsNullOrEmpty(targetPath))
        {
            string baseDirCandidate = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(baseDirCandidate))
            {
                targetPath = baseDirCandidate;
            }
            else if (Environment.ProcessPath is { } procPath && Path.GetDirectoryName(procPath) is { } procDir && File.Exists(Path.Combine(procDir, "appsettings.json")))
            {
                targetPath = Path.Combine(procDir, "appsettings.json");
            }
            else if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")))
            {
                targetPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            }
            else
            {
                targetPath = baseDirCandidate;
            }
        }

        if (File.Exists(targetPath))
        {
            try
            {
                using var stream = File.OpenRead(targetPath);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;

                if (root.TryGetProperty("Cognito", out var cognito))
                {
                    if (cognito.TryGetProperty("ClientId", out var cid) && cid.GetString() is { } cidStr && !string.IsNullOrWhiteSpace(cidStr))
                    {
                        clientId = cidStr.Trim();
                    }

                    if (cognito.TryGetProperty("Region", out var reg) && reg.GetString() is { } regStr && !string.IsNullOrWhiteSpace(regStr))
                    {
                        region = regStr.Trim();
                    }
                }

                if (root.TryGetProperty("WebSocket", out var ws) &&
                    ws.TryGetProperty("EndpointUrl", out var endpoint) &&
                    endpoint.GetString() is { } endpointStr &&
                    !string.IsNullOrWhiteSpace(endpointStr))
                {
                    wsUrl = endpointStr.Trim();
                }
            }
            catch
            {
                // Fallback to defaults on read/parse error
            }
        }

        // 2. Override from environment variables if set
        string? envClientId = Environment.GetEnvironmentVariable("STUDYROOM_COGNITO_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(envClientId))
        {
            clientId = envClientId.Trim();
        }

        string? envRegion = Environment.GetEnvironmentVariable("STUDYROOM_COGNITO_REGION");
        if (!string.IsNullOrWhiteSpace(envRegion))
        {
            region = envRegion.Trim();
        }

        string? envWsUrl = Environment.GetEnvironmentVariable("STUDYROOM_WS_URL");
        if (!string.IsNullOrWhiteSpace(envWsUrl))
        {
            wsUrl = envWsUrl.Trim();
        }

        return new AppConfiguration
        {
            CognitoClientId = clientId,
            CognitoRegion = region,
            WebSocketEndpointUrl = wsUrl
        };
    }
}
