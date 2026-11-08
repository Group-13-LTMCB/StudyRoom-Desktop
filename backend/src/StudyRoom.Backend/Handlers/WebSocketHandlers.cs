using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Runtime;
using StudyRoom.Backend.Services;

namespace StudyRoom.Backend.Handlers;

public class WebSocketHandlers
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly IApiGatewayManagementClientFactory _apiGatewayFactory;
    private readonly string _connectionsTableName;
    private readonly string _dataTableName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WebSocketHandlers() : this(null, null, null, null)
    {
    }

    public WebSocketHandlers(
        IAmazonDynamoDB? dynamoDb,
        IApiGatewayManagementClientFactory? apiGatewayFactory = null,
        string? connectionsTableName = null,
        string? dataTableName = null)
    {
        _dynamoDb = dynamoDb ?? new AmazonDynamoDBClient();
        _apiGatewayFactory = apiGatewayFactory ?? new DefaultApiGatewayManagementClientFactory();
        _connectionsTableName = connectionsTableName ?? Environment.GetEnvironmentVariable("CONNECTIONS_TABLE_NAME") ?? "StudyRoom_Connections";
        _dataTableName = dataTableName ?? Environment.GetEnvironmentVariable("DATA_TABLE_NAME") ?? "StudyRoom_Data";
    }

    public async Task<APIGatewayProxyResponse> HandleAsync(
        APIGatewayProxyRequest request,
        ILambdaContext? context = null)
    {
        var eventType = request.RequestContext?.EventType?.ToUpperInvariant() ?? string.Empty;
        var routeKey = request.RequestContext?.RouteKey ?? string.Empty;

        try
        {
            if (eventType == "CONNECT" || routeKey == "$connect")
            {
                return await HandleConnectAsync(request);
            }

            if (eventType == "DISCONNECT" || routeKey == "$disconnect")
            {
                return await HandleDisconnectAsync(request);
            }

            // Route action from envelope body or routeKey
            string action = routeKey;
            string roomId = string.Empty;
            JsonElement dataElement = default;

            if (!string.IsNullOrWhiteSpace(request.Body))
            {
                using var doc = JsonDocument.Parse(request.Body);
                var root = doc.RootElement;
                if (root.TryGetProperty("action", out var actionProp))
                {
                    action = actionProp.GetString() ?? routeKey;
                }
                if (root.TryGetProperty("roomId", out var roomProp))
                {
                    roomId = roomProp.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("data", out var dataProp))
                {
                    dataElement = dataProp.Clone();
                }
            }

            var endpoint = GetApiGatewayEndpoint(request);
            var apiClient = _apiGatewayFactory.CreateClient(endpoint);

            return action.ToUpperInvariant() switch
            {
                "JOIN_ROOM" => await HandleJoinRoomAsync(request, roomId, dataElement, apiClient),
                "START_POMODORO" => await HandleStartPomodoroAsync(roomId, dataElement, apiClient),
                "PAUSE_POMODORO" => await HandlePausePomodoroAsync(roomId, dataElement, apiClient),
                "PASS_BATON" => await HandlePassBatonAsync(roomId, dataElement, apiClient),
                "MOVE_KANBAN_CARD" => await HandleMoveKanbanCardAsync(roomId, dataElement, apiClient),
                "SEND_CHAT" => await HandleSendChatAsync(roomId, dataElement, apiClient),
                "TRIGGER_FLASHCARD" => await HandleTriggerFlashcardAsync(roomId, dataElement, apiClient),
                _ => new APIGatewayProxyResponse { StatusCode = 200, Body = $"Acknowledged route {action}" }
            };
        }
        catch (Exception ex)
        {
            context?.Logger.LogError($"WebSocket error handling {routeKey}: {ex}");
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Body = ex.Message
            };
        }
    }

    public async Task<APIGatewayProxyResponse> HandleConnectAsync(
        APIGatewayProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionId = request.RequestContext?.ConnectionId ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.ToUnixTimeSeconds() + 86400; // 24h TTL

        string? roomId = null;
        string? userId = null;

        if (request.QueryStringParameters != null)
        {
            request.QueryStringParameters.TryGetValue("roomId", out roomId);
            request.QueryStringParameters.TryGetValue("userId", out userId);
        }

        var item = new Dictionary<string, AttributeValue>
        {
            ["ConnectionId"] = new() { S = connectionId },
            ["ConnectedAt"] = new() { S = now.ToString("o") },
            ["ExpiresAt"] = new() { N = expiresAt.ToString(CultureInfo.InvariantCulture) }
        };

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            item["RoomId"] = new() { S = roomId };
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            item["UserId"] = new() { S = userId };
        }

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _connectionsTableName,
            Item = item
        }, cancellationToken);

        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Connected" };
    }

    public async Task<APIGatewayProxyResponse> HandleDisconnectAsync(
        APIGatewayProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionId = request.RequestContext?.ConnectionId ?? string.Empty;

        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _connectionsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["ConnectionId"] = new() { S = connectionId }
            }
        }, cancellationToken);

        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Disconnected" };
    }

    public async Task<APIGatewayProxyResponse> HandleJoinRoomAsync(
        APIGatewayProxyRequest request,
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        var connectionId = request.RequestContext?.ConnectionId ?? string.Empty;
        var userId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("userId", out var u) ? u.GetString() : string.Empty;
        var displayName = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("displayName", out var d) ? d.GetString() : "Member";

        // Update connection item with RoomId and UserId for GSI1 query lookup
        var updateItem = new UpdateItemRequest
        {
            TableName = _connectionsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["ConnectionId"] = new() { S = connectionId }
            },
            UpdateExpression = "SET RoomId = :r, UserId = :u",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":r"] = new() { S = roomId },
                [":u"] = new() { S = userId ?? string.Empty }
            }
        };

        await _dynamoDb.UpdateItemAsync(updateItem, cancellationToken);

        var broadcastPayload = new
        {
            action = "MEMBER_JOINED",
            roomId,
            data = new
            {
                userId,
                displayName,
                joinedAt = DateTimeOffset.UtcNow.ToString("o")
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Joined" };
    }

    public async Task<APIGatewayProxyResponse> HandleStartPomodoroAsync(
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        var requestedBy = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("requestedBy", out var req) ? req.GetString() : string.Empty;

        // Retrieve group metadata to get duration and host
        var getReq = new GetItemRequest
        {
            TableName = _dataTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = $"GROUP#{roomId}" },
                ["SK"] = new() { S = "METADATA" }
            }
        };
        var getResp = await _dynamoDb.GetItemAsync(getReq, cancellationToken);

        var focusMinutes = 25;
        var currentHostId = requestedBy ?? string.Empty;

        if (getResp.Item != null && getResp.Item.Count > 0)
        {
            if (getResp.Item.TryGetValue("FocusDurationMinutes", out var fVal) && int.TryParse(fVal.N, out var f))
            {
                focusMinutes = f > 0 ? f : 25;
            }
            if (getResp.Item.TryGetValue("CurrentHostId", out var hVal) && !string.IsNullOrWhiteSpace(hVal.S))
            {
                currentHostId = hVal.S;
            }
        }

        var targetEndTime = DateTimeOffset.UtcNow.AddMinutes(focusMinutes).ToString("o");

        var updateReq = new UpdateItemRequest
        {
            TableName = _dataTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = $"GROUP#{roomId}" },
                ["SK"] = new() { S = "METADATA" }
            },
            UpdateExpression = "SET TimerState = :ts, CurrentPhase = :cp, TargetEndTimeUtc = :te",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":ts"] = new() { S = "Running" },
                [":cp"] = new() { S = "Focus" },
                [":te"] = new() { S = targetEndTime }
            }
        };
        await _dynamoDb.UpdateItemAsync(updateReq, cancellationToken);

        var broadcastPayload = new
        {
            action = "POMODORO_SYNC",
            roomId,
            data = new
            {
                currentPhase = "Focus",
                timerState = "Running",
                targetEndTimeUtc = targetEndTime,
                totalDurationSeconds = focusMinutes * 60,
                completedFocusCycles = 0,
                currentHostId
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Started" };
    }

    public async Task<APIGatewayProxyResponse> HandlePausePomodoroAsync(
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        var requestedBy = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("requestedBy", out var req) ? req.GetString() : string.Empty;
        var remainingSeconds = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("remainingSeconds", out var rem) && rem.TryGetInt32(out var rVal) ? rVal : 0;

        var updateReq = new UpdateItemRequest
        {
            TableName = _dataTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = $"GROUP#{roomId}" },
                ["SK"] = new() { S = "METADATA" }
            },
            UpdateExpression = "SET TimerState = :ts REMOVE TargetEndTimeUtc",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":ts"] = new() { S = "Paused" }
            }
        };
        await _dynamoDb.UpdateItemAsync(updateReq, cancellationToken);

        var broadcastPayload = new
        {
            action = "POMODORO_SYNC",
            roomId,
            data = new
            {
                currentPhase = "Focus",
                timerState = "Paused",
                targetEndTimeUtc = (string?)null,
                totalDurationSeconds = remainingSeconds,
                completedFocusCycles = 0,
                currentHostId = requestedBy
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Paused" };
    }

    public async Task<APIGatewayProxyResponse> HandlePassBatonAsync(
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        var currentHostId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("currentHostId", out var ch) ? ch.GetString() ?? string.Empty : string.Empty;
        var newHostUserId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("newHostUserId", out var nh) ? nh.GetString() ?? string.Empty : string.Empty;

        var updateReq = new UpdateItemRequest
        {
            TableName = _dataTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = $"GROUP#{roomId}" },
                ["SK"] = new() { S = "METADATA" }
            },
            UpdateExpression = "SET CurrentHostId = :nh",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":nh"] = new() { S = newHostUserId }
            }
        };
        await _dynamoDb.UpdateItemAsync(updateReq, cancellationToken);

        var broadcastPayload = new
        {
            action = "BATON_PASSED",
            roomId,
            data = new
            {
                previousHostId = currentHostId,
                newHostUserId
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Baton Passed" };
    }

    public async Task<APIGatewayProxyResponse> HandleMoveKanbanCardAsync(
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        var cardId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("cardId", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        var newColumn = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("newColumn", out var col) ? col.GetString() ?? "Todo" : "Todo";
        var newRank = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("newRank", out var rk) && rk.TryGetDouble(out var rVal) ? rVal : 1.0;
        var updatedAt = DateTimeOffset.UtcNow.ToString("o");

        var updateReq = new UpdateItemRequest
        {
            TableName = _dataTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = $"GROUP#{roomId}" },
                ["SK"] = new() { S = $"CARD#{cardId}" }
            },
            UpdateExpression = "SET ColumnId = :col, #rk = :rank, UpdatedAt = :upd",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#rk"] = "Rank"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":col"] = new() { S = newColumn },
                [":rank"] = new() { N = newRank.ToString(CultureInfo.InvariantCulture) },
                [":upd"] = new() { S = updatedAt }
            }
        };
        await _dynamoDb.UpdateItemAsync(updateReq, cancellationToken);

        var broadcastPayload = new
        {
            action = "CARD_MOVED",
            roomId,
            data = new
            {
                cardId,
                newColumn,
                newRank,
                updatedAt
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Card Moved" };
    }

    public async Task<APIGatewayProxyResponse> HandleSendChatAsync(
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        // Spec requirement: Relay in RAM, DO NOT save to DynamoDB!
        var senderId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("senderId", out var s) ? s.GetString() ?? string.Empty : string.Empty;
        var senderName = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("senderName", out var sn) ? sn.GetString() ?? "Member" : "Member";
        var content = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("content", out var c) ? c.GetString() ?? string.Empty : string.Empty;
        var timestamp = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("timestamp", out var t) ? t.GetString() ?? DateTimeOffset.UtcNow.ToString("o") : DateTimeOffset.UtcNow.ToString("o");
        var messageId = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("messageId", out var m) && !string.IsNullOrWhiteSpace(m.GetString()) ? m.GetString()! : Guid.NewGuid().ToString("N");

        var broadcastPayload = new
        {
            action = "CHAT_MESSAGE",
            roomId,
            data = new
            {
                messageId,
                senderId,
                senderName,
                content,
                timestamp
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Chat Relayed" };
    }

    public async Task<APIGatewayProxyResponse> HandleTriggerFlashcardAsync(
        string roomId,
        JsonElement data,
        IAmazonApiGatewayManagementApi apiClient,
        CancellationToken cancellationToken = default)
    {
        // Query StudyRoom_Data for flashcards in the group
        var queryReq = new QueryRequest
        {
            TableName = _dataTableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = $"GROUP#{roomId}" },
                [":skPrefix"] = new() { S = "DECK#" }
            }
        };

        var queryResp = await _dynamoDb.QueryAsync(queryReq, cancellationToken);

        string cardId = "deck_card_001";
        string question = "Deadlock trong lập trình đa luồng xảy ra khi nào?";
        string answer = "Khi hai hay nhiều luồng giữ tài nguyên và cùng chờ tài nguyên của luồng kia mà không nhả ra.";

        if (queryResp.Items != null && queryResp.Items.Count > 0)
        {
            var idx = Random.Shared.Next(queryResp.Items.Count);
            var chosen = queryResp.Items[idx];
            cardId = chosen.GetValueOrDefault("CardId")?.S ?? chosen.GetValueOrDefault("SK")?.S?.Replace("DECK#", "") ?? cardId;
            question = chosen.GetValueOrDefault("Question")?.S ?? question;
            answer = chosen.GetValueOrDefault("Answer")?.S ?? answer;
        }

        var broadcastPayload = new
        {
            action = "FLASHCARD_PROMPT",
            roomId,
            data = new
            {
                cardId,
                question,
                answer,
                timeoutSeconds = 30
            }
        };

        await BroadcastToRoomAsync(apiClient, roomId, broadcastPayload, cancellationToken);
        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Flashcard Triggered" };
    }

    public async Task BroadcastToRoomAsync<T>(
        IAmazonApiGatewayManagementApi apiClient,
        string roomId,
        T message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        // Query GSI1 for connections in the room
        var queryReq = new QueryRequest
        {
            TableName = _connectionsTableName,
            IndexName = "GSI1",
            KeyConditionExpression = "RoomId = :r",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":r"] = new() { S = roomId }
            },
            ProjectionExpression = "ConnectionId"
        };

        var queryResp = await _dynamoDb.QueryAsync(queryReq, cancellationToken);
        if (queryResp.Items == null || queryResp.Items.Count == 0)
        {
            return;
        }

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        foreach (var item in queryResp.Items)
        {
            if (!item.TryGetValue("ConnectionId", out var connVal) || string.IsNullOrWhiteSpace(connVal.S))
            {
                continue;
            }

            var connectionId = connVal.S;
            try
            {
                using var stream = new MemoryStream(payloadBytes);
                var postReq = new PostToConnectionRequest
                {
                    ConnectionId = connectionId,
                    Data = stream
                };
                await apiClient.PostToConnectionAsync(postReq, cancellationToken);
            }
            catch (GoneException)
            {
                // Socket disconnected - clean up dead connection
                await CleanUpConnectionAsync(connectionId, cancellationToken);
            }
            catch (AmazonServiceException ex) when (ex.StatusCode == HttpStatusCode.Gone)
            {
                // Socket disconnected - clean up dead connection
                await CleanUpConnectionAsync(connectionId, cancellationToken);
            }
            catch (Exception)
            {
                // Log and continue broadcasting to other clients in room
            }
        }
    }

    private async Task CleanUpConnectionAsync(string connectionId, CancellationToken cancellationToken)
    {
        try
        {
            await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
            {
                TableName = _connectionsTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["ConnectionId"] = new() { S = connectionId }
                }
            }, cancellationToken);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static string GetApiGatewayEndpoint(APIGatewayProxyRequest request)
    {
        var domain = request.RequestContext?.DomainName;
        var stage = request.RequestContext?.Stage;
        if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(stage))
        {
            return $"https://{domain}/{stage}";
        }
        return "https://localhost/test";
    }
}
