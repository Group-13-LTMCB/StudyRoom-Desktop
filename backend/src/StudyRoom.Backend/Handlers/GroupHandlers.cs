using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using StudyRoom.Backend.Models;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace StudyRoom.Backend.Handlers;

public class GroupHandlers
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _dataTableName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GroupHandlers() : this(null, null)
    {
    }

    public GroupHandlers(IAmazonDynamoDB? dynamoDb, string? dataTableName = null)
    {
        _dynamoDb = dynamoDb ?? new AmazonDynamoDBClient();
        _dataTableName = dataTableName ?? Environment.GetEnvironmentVariable("DATA_TABLE_NAME") ?? "StudyRoom_Data";
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> HandleAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext? context = null)
    {
        var method = request.RequestContext?.Http?.Method?.ToUpperInvariant() ?? "GET";
        var rawPath = request.RawPath ?? request.RequestContext?.Http?.Path ?? string.Empty;
        var routeKey = request.RouteKey ?? string.Empty;

        try
        {
            if (routeKey == "POST /groups" || (method == "POST" && (rawPath.Equals("/groups", StringComparison.OrdinalIgnoreCase) || rawPath.Equals("/groups/", StringComparison.OrdinalIgnoreCase))))
            {
                return await CreateGroupAsync(request);
            }

            if (routeKey == "POST /groups/{id}/members" || (method == "POST" && rawPath.Contains("/members", StringComparison.OrdinalIgnoreCase)))
            {
                var groupId = ExtractGroupId(request, rawPath, "/members");
                return await AddMemberAsync(groupId, request);
            }

            if (routeKey == "GET /groups/{id}" || (method == "GET" && rawPath.StartsWith("/groups/", StringComparison.OrdinalIgnoreCase)))
            {
                var groupId = ExtractGroupId(request, rawPath, null);
                return await GetGroupAsync(groupId);
            }

            return BuildResponse(HttpStatusCode.NotFound, new { error = $"Route not found for {method} {rawPath}" });
        }
        catch (ArgumentException ex)
        {
            return BuildResponse(HttpStatusCode.BadRequest, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            context?.Logger.LogError($"Error processing request: {ex}");
            return BuildResponse(HttpStatusCode.InternalServerError, new { error = "An internal server error occurred" });
        }
    }

    public async Task<APIGatewayProxyResponse> HandleV1Async(
        APIGatewayProxyRequest request,
        ILambdaContext? context = null)
    {
        var v2Req = new APIGatewayHttpApiV2ProxyRequest
        {
            RawPath = request.Path,
            Body = request.Body,
            Headers = request.Headers,
            PathParameters = request.PathParameters,
            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
            {
                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                {
                    Method = request.HttpMethod,
                    Path = request.Path
                }
            }
        };

        var v2Resp = await HandleAsync(v2Req, context);
        return new APIGatewayProxyResponse
        {
            StatusCode = v2Resp.StatusCode,
            Headers = v2Resp.Headers,
            Body = v2Resp.Body
        };
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> CreateGroupAsync(
        APIGatewayHttpApiV2ProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = DeserializeBody<CreateGroupRequest>(request.Body);
        if (body == null || string.IsNullOrWhiteSpace(body.GroupName))
        {
            return BuildResponse(HttpStatusCode.BadRequest, new { error = "groupName is required" });
        }

        var userId = body.OwnerId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = ExtractUserId(request) ?? Guid.NewGuid().ToString("N");
        }

        var groupId = Guid.NewGuid().ToString("N");
        var joinCode = GenerateJoinCode();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var controlMode = NormalizeControlMode(body.ControlMode);
        var focusMinutes = body.FocusDurationMinutes > 0 ? body.FocusDurationMinutes : 25;
        var breakMinutes = body.BreakDurationMinutes > 0 ? body.BreakDurationMinutes : 5;
        var displayName = !string.IsNullOrWhiteSpace(body.OwnerDisplayName) ? body.OwnerDisplayName : "Host";

        var metadataItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = $"GROUP#{groupId}" },
            ["SK"] = new() { S = "METADATA" },
            ["GroupId"] = new() { S = groupId },
            ["GroupName"] = new() { S = body.GroupName },
            ["OwnerId"] = new() { S = userId },
            ["ControlMode"] = new() { S = controlMode },
            ["CurrentHostId"] = new() { S = userId },
            ["FocusDurationMinutes"] = new() { N = focusMinutes.ToString(CultureInfo.InvariantCulture) },
            ["BreakDurationMinutes"] = new() { N = breakMinutes.ToString(CultureInfo.InvariantCulture) },
            ["CurrentPhase"] = new() { S = "Focus" },
            ["TimerState"] = new() { S = "Stopped" },
            ["CreatedAt"] = new() { S = now },
            ["JoinCode"] = new() { S = joinCode }
        };

        var memberItem = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = $"GROUP#{groupId}" },
            ["SK"] = new() { S = $"MEMBER#{userId}" },
            ["UserId"] = new() { S = userId },
            ["Role"] = new() { S = "Host" },
            ["DisplayName"] = new() { S = displayName },
            ["JoinedAt"] = new() { S = now }
        };

        var transactRequest = new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem { Put = new Put { TableName = _dataTableName, Item = metadataItem } },
                new TransactWriteItem { Put = new Put { TableName = _dataTableName, Item = memberItem } }
            ]
        };

        await _dynamoDb.TransactWriteItemsAsync(transactRequest, cancellationToken);

        var response = new CreateGroupResponse
        {
            GroupId = groupId,
            GroupName = body.GroupName,
            OwnerId = userId,
            JoinCode = joinCode
        };

        return BuildResponse(HttpStatusCode.OK, response);
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> GetGroupAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return BuildResponse(HttpStatusCode.BadRequest, new { error = "Group ID is required" });
        }

        var queryRequest = new QueryRequest
        {
            TableName = _dataTableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = $"GROUP#{groupId}" }
            }
        };

        var queryResponse = await _dynamoDb.QueryAsync(queryRequest, cancellationToken);
        if (queryResponse.Items == null || queryResponse.Items.Count == 0)
        {
            return BuildResponse(HttpStatusCode.NotFound, new { error = "Group not found" });
        }

        GroupMetadataDto? metadata = null;
        var members = new List<MemberDto>();
        var cards = new List<KanbanCardDto>();
        var deck = new List<FlashcardDto>();

        foreach (var item in queryResponse.Items)
        {
            var sk = item.GetValueOrDefault("SK")?.S ?? string.Empty;

            if (sk == "METADATA")
            {
                metadata = new GroupMetadataDto
                {
                    GroupId = item.GetValueOrDefault("GroupId")?.S ?? groupId,
                    GroupName = item.GetValueOrDefault("GroupName")?.S ?? string.Empty,
                    OwnerId = item.GetValueOrDefault("OwnerId")?.S ?? string.Empty,
                    ControlMode = item.GetValueOrDefault("ControlMode")?.S ?? "HOST_ONLY",
                    CurrentHostId = item.GetValueOrDefault("CurrentHostId")?.S ?? string.Empty,
                    FocusDurationMinutes = int.TryParse(item.GetValueOrDefault("FocusDurationMinutes")?.N, out var f) ? f : 25,
                    BreakDurationMinutes = int.TryParse(item.GetValueOrDefault("BreakDurationMinutes")?.N, out var b) ? b : 5,
                    CurrentPhase = item.GetValueOrDefault("CurrentPhase")?.S ?? "Focus",
                    TimerState = item.GetValueOrDefault("TimerState")?.S ?? "Stopped",
                    TargetEndTimeUtc = item.GetValueOrDefault("TargetEndTimeUtc")?.S,
                    CreatedAt = item.GetValueOrDefault("CreatedAt")?.S ?? string.Empty,
                    JoinCode = item.GetValueOrDefault("JoinCode")?.S
                };
            }
            else if (sk.StartsWith("MEMBER#", StringComparison.OrdinalIgnoreCase))
            {
                members.Add(new MemberDto
                {
                    UserId = item.GetValueOrDefault("UserId")?.S ?? sk.Substring("MEMBER#".Length),
                    Role = item.GetValueOrDefault("Role")?.S ?? "Member",
                    DisplayName = item.GetValueOrDefault("DisplayName")?.S ?? string.Empty,
                    AvatarUrl = item.GetValueOrDefault("AvatarUrl")?.S,
                    JoinedAt = item.GetValueOrDefault("JoinedAt")?.S ?? string.Empty
                });
            }
            else if (sk.StartsWith("CARD#", StringComparison.OrdinalIgnoreCase))
            {
                cards.Add(new KanbanCardDto
                {
                    CardId = item.GetValueOrDefault("CardId")?.S ?? sk.Substring("CARD#".Length),
                    ColumnId = item.GetValueOrDefault("ColumnId")?.S ?? "Todo",
                    Title = item.GetValueOrDefault("Title")?.S ?? string.Empty,
                    Description = item.GetValueOrDefault("Description")?.S,
                    Rank = double.TryParse(item.GetValueOrDefault("Rank")?.N, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 1.0,
                    AssigneeId = item.GetValueOrDefault("AssigneeId")?.S,
                    CreatedAt = item.GetValueOrDefault("CreatedAt")?.S ?? string.Empty,
                    UpdatedAt = item.GetValueOrDefault("UpdatedAt")?.S ?? string.Empty
                });
            }
            else if (sk.StartsWith("DECK#", StringComparison.OrdinalIgnoreCase))
            {
                deck.Add(new FlashcardDto
                {
                    CardId = item.GetValueOrDefault("CardId")?.S ?? sk.Substring("DECK#".Length),
                    Question = item.GetValueOrDefault("Question")?.S ?? string.Empty,
                    Answer = item.GetValueOrDefault("Answer")?.S ?? string.Empty,
                    CreatedBy = item.GetValueOrDefault("CreatedBy")?.S ?? string.Empty,
                    CreatedAt = item.GetValueOrDefault("CreatedAt")?.S ?? string.Empty,
                    LastReviewedAt = item.GetValueOrDefault("LastReviewedAt")?.S
                });
            }
        }

        if (metadata == null)
        {
            return BuildResponse(HttpStatusCode.NotFound, new { error = "Group metadata not found" });
        }

        var result = new GroupDetailResponse
        {
            Group = metadata,
            Members = members,
            Cards = cards,
            Deck = deck
        };

        return BuildResponse(HttpStatusCode.OK, result);
    }

    public async Task<APIGatewayHttpApiV2ProxyResponse> AddMemberAsync(
        string groupId,
        APIGatewayHttpApiV2ProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return BuildResponse(HttpStatusCode.BadRequest, new { error = "Group ID is required" });
        }

        var body = DeserializeBody<AddMemberRequest>(request.Body);
        var displayName = body?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "Member";
        }

        var userId = body?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = ExtractUserId(request) ?? Guid.NewGuid().ToString("N");
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var putItemRequest = new PutItemRequest
        {
            TableName = _dataTableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = $"GROUP#{groupId}" },
                ["SK"] = new() { S = $"MEMBER#{userId}" },
                ["UserId"] = new() { S = userId },
                ["Role"] = new() { S = "Member" },
                ["DisplayName"] = new() { S = displayName },
                ["JoinedAt"] = new() { S = now }
            }
        };

        await _dynamoDb.PutItemAsync(putItemRequest, cancellationToken);

        var response = new AddMemberResponse
        {
            Success = true,
            Role = "Member"
        };

        return BuildResponse(HttpStatusCode.OK, response);
    }

    private static string ExtractGroupId(APIGatewayHttpApiV2ProxyRequest request, string rawPath, string? suffixToRemove)
    {
        if (request.PathParameters != null && request.PathParameters.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        var path = rawPath.Trim('/');
        if (suffixToRemove != null && path.EndsWith(suffixToRemove.Trim('/'), StringComparison.OrdinalIgnoreCase))
        {
            var idx = path.LastIndexOf(suffixToRemove.Trim('/'), StringComparison.OrdinalIgnoreCase);
            path = path.Substring(0, idx).Trim('/');
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }

    private static string? ExtractUserId(APIGatewayHttpApiV2ProxyRequest request)
    {
        if (request.RequestContext?.Authorizer?.Jwt?.Claims != null)
        {
            var claims = request.RequestContext.Authorizer.Jwt.Claims;
            if (claims.TryGetValue("sub", out var sub) && !string.IsNullOrWhiteSpace(sub))
            {
                return sub;
            }
            if (claims.TryGetValue("cognito:username", out var user) && !string.IsNullOrWhiteSpace(user))
            {
                return user;
            }
        }

        if (request.Headers != null)
        {
            if (request.Headers.TryGetValue("x-user-id", out var h) && !string.IsNullOrWhiteSpace(h))
            {
                return h;
            }
            if (request.Headers.TryGetValue("X-User-Id", out var h2) && !string.IsNullOrWhiteSpace(h2))
            {
                return h2;
            }
        }

        return null;
    }

    private static string NormalizeControlMode(string? mode)
    {
        if (string.Equals(mode, "Equal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "EQUAL", StringComparison.OrdinalIgnoreCase))
        {
            return "EQUAL";
        }
        return "HOST_ONLY";
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var buffer = new char[6];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = chars[Random.Shared.Next(chars.Length)];
        }
        return new string(buffer);
    }

    private static T? DeserializeBody<T>(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }
        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static APIGatewayHttpApiV2ProxyResponse BuildResponse<T>(HttpStatusCode statusCode, T data)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)statusCode,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            Body = JsonSerializer.Serialize(data, JsonOptions)
        };
    }
}
