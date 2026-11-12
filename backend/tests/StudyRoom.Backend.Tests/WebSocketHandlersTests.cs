using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Runtime;
using Moq;
using StudyRoom.Backend.Handlers;
using StudyRoom.Backend.Services;
using Xunit;

namespace StudyRoom.Backend.Tests;

public class WebSocketHandlersTests
{
    private readonly Mock<IAmazonDynamoDB> _dynamoDbMock;
    private readonly Mock<IApiGatewayManagementClientFactory> _factoryMock;
    private readonly Mock<IAmazonApiGatewayManagementApi> _apiClientMock;
    private readonly WebSocketHandlers _handlers;

    private const string ConnectionsTable = "Test_StudyRoom_Connections";
    private const string DataTable = "Test_StudyRoom_Data";

    public WebSocketHandlersTests()
    {
        _dynamoDbMock = new Mock<IAmazonDynamoDB>();
        _factoryMock = new Mock<IApiGatewayManagementClientFactory>();
        _apiClientMock = new Mock<IAmazonApiGatewayManagementApi>();

        _factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_apiClientMock.Object);

        _handlers = new WebSocketHandlers(
            _dynamoDbMock.Object,
            _factoryMock.Object,
            ConnectionsTable,
            DataTable);
    }

    [Fact]
    public async Task Connect_ValidRequest_SavesConnectionWith24hTTL_Returns200()
    {
        PutItemRequest? capturedPut = null;
        _dynamoDbMock
            .Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedPut = req)
            .ReturnsAsync(new PutItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "CONNECT",
                RouteKey = "$connect",
                ConnectionId = "conn_123"
            },
            QueryStringParameters = new Dictionary<string, string>
            {
                ["roomId"] = "room_abc",
                ["userId"] = "user_xyz"
            }
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Connected", response.Body);
        Assert.NotNull(capturedPut);
        Assert.Equal(ConnectionsTable, capturedPut!.TableName);
        Assert.Equal("conn_123", capturedPut.Item["ConnectionId"].S);
        Assert.Equal("room_abc", capturedPut.Item["RoomId"].S);
        Assert.Equal("user_xyz", capturedPut.Item["UserId"].S);
        Assert.True(long.TryParse(capturedPut.Item["ExpiresAt"].N, out var expiresAt));
        Assert.True(expiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 80000);
    }

    [Fact]
    public async Task Disconnect_ValidRequest_DeletesConnection_Returns200()
    {
        DeleteItemRequest? capturedDelete = null;
        _dynamoDbMock
            .Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteItemRequest, CancellationToken>((req, _) => capturedDelete = req)
            .ReturnsAsync(new DeleteItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "DISCONNECT",
                RouteKey = "$disconnect",
                ConnectionId = "conn_123"
            }
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Disconnected", response.Body);
        Assert.NotNull(capturedDelete);
        Assert.Equal(ConnectionsTable, capturedDelete!.TableName);
        Assert.Equal("conn_123", capturedDelete.Key["ConnectionId"].S);
    }

    [Fact]
    public async Task JoinRoom_UpdatesConnectionAndBroadcastsMemberJoined()
    {
        SetupRoomConnections(["conn_123", "conn_456"]);

        UpdateItemRequest? capturedUpdate = null;
        _dynamoDbMock
            .Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedUpdate = req)
            .ReturnsAsync(new UpdateItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "JOIN_ROOM",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "JOIN_ROOM",
                roomId = "room_abc",
                data = new
                {
                    userId = "user_alice",
                    displayName = "Alice"
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedUpdate);
        Assert.Equal("conn_123", capturedUpdate!.Key["ConnectionId"].S);
        Assert.Equal("room_abc", capturedUpdate.ExpressionAttributeValues[":r"].S);
        Assert.Equal("user_alice", capturedUpdate.ExpressionAttributeValues[":u"].S);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123" || p.ConnectionId == "conn_456"),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StartPomodoro_UpdatesMetadataAndBroadcastsPomodoroSync()
    {
        SetupRoomConnections(["conn_123"]);

        _dynamoDbMock
            .Setup(x => x.GetItemAsync(It.IsAny<GetItemRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetItemResponse
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["FocusDurationMinutes"] = new() { N = "25" },
                    ["CurrentHostId"] = new() { S = "user_host" }
                }
            });

        UpdateItemRequest? capturedUpdate = null;
        _dynamoDbMock
            .Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedUpdate = req)
            .ReturnsAsync(new UpdateItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "START_POMODORO",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "START_POMODORO",
                roomId = "room_abc",
                data = new
                {
                    requestedBy = "user_host"
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedUpdate);
        Assert.Equal("Running", capturedUpdate!.ExpressionAttributeValues[":ts"].S);
        Assert.Equal("Focus", capturedUpdate.ExpressionAttributeValues[":cp"].S);
        Assert.NotNull(capturedUpdate.ExpressionAttributeValues[":te"].S);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PausePomodoro_UpdatesMetadataAndBroadcastsPausedState()
    {
        SetupRoomConnections(["conn_123"]);

        UpdateItemRequest? capturedUpdate = null;
        _dynamoDbMock
            .Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedUpdate = req)
            .ReturnsAsync(new UpdateItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "PAUSE_POMODORO",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "PAUSE_POMODORO",
                roomId = "room_abc",
                data = new
                {
                    requestedBy = "user_host",
                    remainingSeconds = 850
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedUpdate);
        Assert.Equal("Paused", capturedUpdate!.ExpressionAttributeValues[":ts"].S);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PassBaton_UpdatesCurrentHostIdAndBroadcasts()
    {
        SetupRoomConnections(["conn_123"]);

        UpdateItemRequest? capturedUpdate = null;
        _dynamoDbMock
            .Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedUpdate = req)
            .ReturnsAsync(new UpdateItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "PASS_BATON",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "PASS_BATON",
                roomId = "room_abc",
                data = new
                {
                    currentHostId = "user_old_host",
                    newHostUserId = "user_new_host"
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedUpdate);
        Assert.Equal("user_new_host", capturedUpdate!.ExpressionAttributeValues[":nh"].S);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveKanbanCard_UpdatesDynamoDbWithReservedWordEscaping_BroadcastsCardMoved()
    {
        SetupRoomConnections(["conn_123"]);

        UpdateItemRequest? capturedUpdate = null;
        _dynamoDbMock
            .Setup(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((req, _) => capturedUpdate = req)
            .ReturnsAsync(new UpdateItemResponse());

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "MOVE_KANBAN_CARD",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "MOVE_KANBAN_CARD",
                roomId = "room_abc",
                data = new
                {
                    cardId = "card_987",
                    newColumn = "InProgress",
                    newRank = 1.75,
                    movedBy = "user_alice"
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedUpdate);
        Assert.Equal("CARD#card_987", capturedUpdate!.Key["SK"].S);
        Assert.Equal("InProgress", capturedUpdate.ExpressionAttributeValues[":col"].S);
        Assert.Equal("1.75", capturedUpdate.ExpressionAttributeValues[":rank"].N);
        Assert.Equal("Rank", capturedUpdate.ExpressionAttributeNames["#rk"]);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendChat_RelaysInRamOnly_NeverCallsDynamoDbWrite()
    {
        SetupRoomConnections(["conn_123", "conn_456"]);

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "SEND_CHAT",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "SEND_CHAT",
                roomId = "room_abc",
                data = new
                {
                    senderId = "user_alice",
                    senderName = "Alice",
                    content = "Let's focus!"
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);

        // Crucial requirement: RAM relay only - no DynamoDB writes!
        _dynamoDbMock.Verify(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _dynamoDbMock.Verify(x => x.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123" || p.ConnectionId == "conn_456"),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TriggerFlashcard_PicksCardAndBroadcasts()
    {
        SetupRoomConnections(["conn_123"]);

        _dynamoDbMock
            .Setup(x => x.QueryAsync(It.Is<QueryRequest>(q => q.ExpressionAttributeValues != null && q.ExpressionAttributeValues.ContainsKey(":skPrefix") && q.ExpressionAttributeValues[":skPrefix"].S == "DECK#"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse
            {
                Items =
                [
                    new Dictionary<string, AttributeValue>
                    {
                        ["CardId"] = new() { S = "deck_card_007" },
                        ["Question"] = new() { S = "What is a deadlock?" },
                        ["Answer"] = new() { S = "Mutual waiting condition" }
                    }
                ]
            });

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "TRIGGER_FLASHCARD",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "TRIGGER_FLASHCARD",
                roomId = "room_abc",
                data = new
                {
                    triggeredBy = "user_alice"
                }
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Broadcast_CatchesGoneException_DeletesDeadConnection()
    {
        SetupRoomConnections(["dead_socket_1", "live_socket_2"]);

        // First socket throws GoneException
        _apiClientMock
            .Setup(x => x.PostToConnectionAsync(It.Is<PostToConnectionRequest>(p => p.ConnectionId == "dead_socket_1"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoneException("Socket gone"));

        // Second socket succeeds
        _apiClientMock
            .Setup(x => x.PostToConnectionAsync(It.Is<PostToConnectionRequest>(p => p.ConnectionId == "live_socket_2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostToConnectionResponse());

        DeleteItemRequest? capturedDelete = null;
        _dynamoDbMock
            .Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteItemRequest, CancellationToken>((req, _) => capturedDelete = req)
            .ReturnsAsync(new DeleteItemResponse());

        await _handlers.BroadcastToRoomAsync(_apiClientMock.Object, "room_abc", new { message = "hello" });

        // Verifies dead socket is cleaned up from StudyRoom_Connections
        Assert.NotNull(capturedDelete);
        Assert.Equal(ConnectionsTable, capturedDelete!.TableName);
        Assert.Equal("dead_socket_1", capturedDelete.Key["ConnectionId"].S);

        // Verifies second socket was still sent message
        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "live_socket_2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Broadcast_CatchesAmazonServiceExceptionGone_DeletesDeadConnection()
    {
        SetupRoomConnections(["dead_socket_gone"]);

        var goneException = new AmazonServiceException("Gone")
        {
            StatusCode = HttpStatusCode.Gone
        };

        _apiClientMock
            .Setup(x => x.PostToConnectionAsync(It.IsAny<PostToConnectionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(goneException);

        DeleteItemRequest? capturedDelete = null;
        _dynamoDbMock
            .Setup(x => x.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteItemRequest, CancellationToken>((req, _) => capturedDelete = req)
            .ReturnsAsync(new DeleteItemResponse());

        await _handlers.BroadcastToRoomAsync(_apiClientMock.Object, "room_abc", new { message = "hello" });

        Assert.NotNull(capturedDelete);
        Assert.Equal("dead_socket_gone", capturedDelete!.Key["ConnectionId"].S);
    }

    [Fact]
    public async Task TriggerFlashcard_NoCardsInDeck_BroadcastsDefaultPrompt()
    {
        SetupRoomConnections(["conn_123"]);

        _dynamoDbMock
            .Setup(x => x.QueryAsync(It.Is<QueryRequest>(q => q.ExpressionAttributeValues != null && q.ExpressionAttributeValues.ContainsKey(":skPrefix")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = [] });

        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "TRIGGER_FLASHCARD",
                ConnectionId = "conn_123",
                DomainName = "api.studyroom.test",
                Stage = "prod"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "TRIGGER_FLASHCARD",
                roomId = "room_abc"
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.Is<PostToConnectionRequest>(p => p.ConnectionId == "conn_123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Broadcast_NoConnectionsInRoom_DoesNotInvokePostToConnection()
    {
        SetupRoomConnections([]);

        await _handlers.BroadcastToRoomAsync(_apiClientMock.Object, "room_empty", new { test = "empty" });

        _apiClientMock.Verify(x => x.PostToConnectionAsync(
            It.IsAny<PostToConnectionRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnrecognizedRoute_AcknowledgesGracefully()
    {
        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                EventType = "MESSAGE",
                RouteKey = "UNKNOWN_CUSTOM_ACTION"
            },
            Body = JsonSerializer.Serialize(new
            {
                action = "UNKNOWN_CUSTOM_ACTION",
                roomId = "room_abc"
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Acknowledged route", response.Body);
    }

    private void SetupRoomConnections(IEnumerable<string> connectionIds)
    {
        var items = new List<Dictionary<string, AttributeValue>>();
        foreach (var id in connectionIds)
        {
            items.Add(new Dictionary<string, AttributeValue>
            {
                ["ConnectionId"] = new() { S = id }
            });
        }

        _dynamoDbMock
            .Setup(x => x.QueryAsync(It.Is<QueryRequest>(q => q.IndexName == "GSI1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = items });
    }
}
