using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Moq;
using StudyRoom.Backend.Handlers;
using StudyRoom.Backend.Models;
using Xunit;

namespace StudyRoom.Backend.Tests;

public class GroupHandlersTests
{
    private readonly Mock<IAmazonDynamoDB> _dynamoDbMock;
    private readonly GroupHandlers _handlers;

    public GroupHandlersTests()
    {
        _dynamoDbMock = new Mock<IAmazonDynamoDB>();
        _handlers = new GroupHandlers(_dynamoDbMock.Object, "Test_StudyRoom_Data");
    }

    [Fact]
    public async Task CreateGroup_ValidRequest_WritesMetadataAndMember_ReturnsOk()
    {
        TransactWriteItemsRequest? capturedTransact = null;
        _dynamoDbMock
            .Setup(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((req, _) => capturedTransact = req)
            .ReturnsAsync(new TransactWriteItemsResponse());

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "POST /groups",
            RawPath = "/groups",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST" }
            },
            Body = JsonSerializer.Serialize(new CreateGroupRequest
            {
                GroupName = "Deep Work Squad",
                ControlMode = "Equal",
                FocusDurationMinutes = 30,
                BreakDurationMinutes = 10,
                OwnerId = "user_host_123",
                OwnerDisplayName = "Lead Dev"
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedTransact);
        Assert.Equal(2, capturedTransact!.TransactItems.Count);

        var metadataItem = capturedTransact.TransactItems[0].Put.Item;
        var memberItem = capturedTransact.TransactItems[1].Put.Item;

        Assert.Equal("METADATA", metadataItem["SK"].S);
        Assert.Equal("Deep Work Squad", metadataItem["GroupName"].S);
        Assert.Equal("EQUAL", metadataItem["ControlMode"].S);
        Assert.Equal("30", metadataItem["FocusDurationMinutes"].N);
        Assert.Equal("10", metadataItem["BreakDurationMinutes"].N);
        Assert.Equal("user_host_123", metadataItem["OwnerId"].S);

        Assert.Equal("MEMBER#user_host_123", memberItem["SK"].S);
        Assert.Equal("Host", memberItem["Role"].S);
        Assert.Equal("Lead Dev", memberItem["DisplayName"].S);

        var responseBody = JsonSerializer.Deserialize<CreateGroupResponse>(response.Body);
        Assert.NotNull(responseBody);
        Assert.Equal("Deep Work Squad", responseBody!.GroupName);
        Assert.Equal("user_host_123", responseBody.OwnerId);
        Assert.False(string.IsNullOrWhiteSpace(responseBody.GroupId));
        Assert.False(string.IsNullOrWhiteSpace(responseBody.JoinCode));
    }

    [Fact]
    public async Task CreateGroup_MissingGroupName_ReturnsBadRequest()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "POST /groups",
            RawPath = "/groups",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST" }
            },
            Body = JsonSerializer.Serialize(new CreateGroupRequest
            {
                GroupName = ""
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("groupName is required", response.Body);
    }

    [Fact]
    public async Task GetGroup_ExistingGroup_ReturnsGroupDetailsWithMembersCardsAndDeck()
    {
        const string groupId = "room_abc123";
        var queryResponse = new QueryResponse
        {
            Items =
            [
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = $"GROUP#{groupId}" },
                    ["SK"] = new() { S = "METADATA" },
                    ["GroupId"] = new() { S = groupId },
                    ["GroupName"] = new() { S = "Algorithms 101" },
                    ["OwnerId"] = new() { S = "user_owner_01" },
                    ["ControlMode"] = new() { S = "HOST_ONLY" },
                    ["CurrentHostId"] = new() { S = "user_owner_01" },
                    ["FocusDurationMinutes"] = new() { N = "25" },
                    ["BreakDurationMinutes"] = new() { N = "5" },
                    ["CurrentPhase"] = new() { S = "Focus" },
                    ["TimerState"] = new() { S = "Running" },
                    ["CreatedAt"] = new() { S = "2026-10-26T08:00:00Z" }
                },
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = $"GROUP#{groupId}" },
                    ["SK"] = new() { S = "MEMBER#user_owner_01" },
                    ["UserId"] = new() { S = "user_owner_01" },
                    ["Role"] = new() { S = "Host" },
                    ["DisplayName"] = new() { S = "Owner A" },
                    ["JoinedAt"] = new() { S = "2026-10-26T08:00:00Z" }
                },
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = $"GROUP#{groupId}" },
                    ["SK"] = new() { S = "CARD#card_k1" },
                    ["CardId"] = new() { S = "card_k1" },
                    ["ColumnId"] = new() { S = "InProgress" },
                    ["Title"] = new() { S = "Implement Tree" },
                    ["Rank"] = new() { N = "1.5" },
                    ["CreatedAt"] = new() { S = "2026-10-26T08:02:00Z" },
                    ["UpdatedAt"] = new() { S = "2026-10-26T08:05:00Z" }
                },
                new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = $"GROUP#{groupId}" },
                    ["SK"] = new() { S = "DECK#card_d1" },
                    ["CardId"] = new() { S = "card_d1" },
                    ["Question"] = new() { S = "What is BFS?" },
                    ["Answer"] = new() { S = "Breadth First Search" },
                    ["CreatedBy"] = new() { S = "user_owner_01" },
                    ["CreatedAt"] = new() { S = "2026-10-26T08:01:00Z" }
                }
            ]
        };

        _dynamoDbMock
            .Setup(x => x.QueryAsync(It.Is<QueryRequest>(q => q.ExpressionAttributeValues[":pk"].S == $"GROUP#{groupId}"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResponse);

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "GET /groups/{id}",
            PathParameters = new Dictionary<string, string> { ["id"] = groupId },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "GET" }
            }
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        var detail = JsonSerializer.Deserialize<GroupDetailResponse>(response.Body);
        Assert.NotNull(detail);
        Assert.Equal("Algorithms 101", detail!.Group.GroupName);
        Assert.Equal("Running", detail.Group.TimerState);
        Assert.Single(detail.Members);
        Assert.Equal("Owner A", detail.Members[0].DisplayName);
        Assert.Single(detail.Cards);
        Assert.Equal("Implement Tree", detail.Cards[0].Title);
        Assert.Equal(1.5, detail.Cards[0].Rank);
        Assert.Single(detail.Deck);
        Assert.Equal("What is BFS?", detail.Deck[0].Question);
    }

    [Fact]
    public async Task GetGroup_NotFound_Returns404()
    {
        _dynamoDbMock
            .Setup(x => x.QueryAsync(It.IsAny<QueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResponse { Items = [] });

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "GET /groups/{id}",
            PathParameters = new Dictionary<string, string> { ["id"] = "non_existent_room" },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "GET" }
            }
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Group not found", response.Body);
    }

    [Fact]
    public async Task AddMember_ValidRequest_WritesMemberItem_ReturnsOk()
    {
        PutItemRequest? capturedPut = null;
        _dynamoDbMock
            .Setup(x => x.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((req, _) => capturedPut = req)
            .ReturnsAsync(new PutItemResponse());

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "POST /groups/{id}/members",
            PathParameters = new Dictionary<string, string> { ["id"] = "room_123" },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST" }
            },
            Body = JsonSerializer.Serialize(new AddMemberRequest
            {
                DisplayName = "Bob",
                UserId = "user_bob_456"
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedPut);
        Assert.Equal("GROUP#room_123", capturedPut!.Item["PK"].S);
        Assert.Equal("MEMBER#user_bob_456", capturedPut.Item["SK"].S);
        Assert.Equal("Bob", capturedPut.Item["DisplayName"].S);
        Assert.Equal("Member", capturedPut.Item["Role"].S);

        var result = JsonSerializer.Deserialize<AddMemberResponse>(response.Body);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("Member", result.Role);
    }

    [Fact]
    public async Task HandleV1Async_ProxyRequest_TranslatesSuccessfully()
    {
        _dynamoDbMock
            .Setup(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactWriteItemsResponse());

        var v1Request = new APIGatewayProxyRequest
        {
            HttpMethod = "POST",
            Path = "/groups",
            Body = JsonSerializer.Serialize(new CreateGroupRequest
            {
                GroupName = "V1 Squad"
            })
        };

        var v1Response = await _handlers.HandleV1Async(v1Request);

        Assert.Equal(200, v1Response.StatusCode);
        Assert.Contains("V1 Squad", v1Response.Body);
    }

    [Fact]
    public async Task CreateGroup_ExtractsUserIdFromClaims_WhenNotProvidedInBody()
    {
        TransactWriteItemsRequest? capturedTransact = null;
        _dynamoDbMock
            .Setup(x => x.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((req, _) => capturedTransact = req)
            .ReturnsAsync(new TransactWriteItemsResponse());

        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "POST /groups",
            RawPath = "/groups",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "POST" },
                Authorizer = new()
                {
                    Jwt = new()
                    {
                        Claims = new Dictionary<string, string>
                        {
                            ["sub"] = "cognito_sub_claim_123"
                        }
                    }
                }
            },
            Body = JsonSerializer.Serialize(new CreateGroupRequest
            {
                GroupName = "Token Auth Squad"
            })
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(capturedTransact);
        var memberItem = capturedTransact!.TransactItems[1].Put.Item;
        Assert.Equal("MEMBER#cognito_sub_claim_123", memberItem["SK"].S);
    }

    [Fact]
    public async Task GetGroup_EmptyGroupId_ReturnsBadRequest()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "GET /groups/{id}",
            PathParameters = new Dictionary<string, string> { ["id"] = "" },
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "GET" }
            }
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Group ID is required", response.Body);
    }

    [Fact]
    public async Task HandleAsync_UnrecognizedRoute_Returns404()
    {
        var request = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = "DELETE /unknown",
            RawPath = "/unknown",
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "DELETE" }
            }
        };

        var response = await _handlers.HandleAsync(request);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains("Route not found", response.Body);
    }
}
