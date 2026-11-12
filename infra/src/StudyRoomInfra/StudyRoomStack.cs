using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace StudyRoomInfra;

public class StudyRoomStack : Stack
{
    public StudyRoomStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // 1. DynamoDB: StudyRoom_Connections
        var connectionsTable = new Table(this, "StudyRoomConnectionsTable", new TableProps
        {
            TableName = "StudyRoom_Connections",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "ConnectionId",
                Type = AttributeType.STRING
            },
            TimeToLiveAttribute = "ExpiresAt",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        connectionsTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "GSI1",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "RoomId",
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "ConnectionId",
                Type = AttributeType.STRING
            },
            ProjectionType = ProjectionType.ALL
        });

        // 2. DynamoDB: StudyRoom_Data (Single-Table Design)
        var dataTable = new Table(this, "StudyRoomDataTable", new TableProps
        {
            TableName = "StudyRoom_Data",
            PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "PK",
                Type = AttributeType.STRING
            },
            SortKey = new Amazon.CDK.AWS.DynamoDB.Attribute
            {
                Name = "SK",
                Type = AttributeType.STRING
            },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // 3. Cognito User Pool & App Client without secret
        var userPool = new UserPool(this, "StudyRoomUserPool", new UserPoolProps
        {
            UserPoolName = "StudyRoomUserPool",
            SignInAliases = new SignInAliases
            {
                Email = true,
                Username = false
            },
            AutoVerify = new AutoVerifiedAttrs
            {
                Email = true
            },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            Mfa = Mfa.OPTIONAL,
            MfaSecondFactor = new MfaSecondFactor
            {
                Sms = false,
                Otp = true
            },
            SelfSignUpEnabled = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        var appClient = new UserPoolClient(this, "StudyRoomAppClient", new UserPoolClientProps
        {
            UserPool = userPool,
            UserPoolClientName = "StudyRoomAppClient",
            GenerateSecret = false,
            AuthFlows = new AuthFlow
            {
                UserPassword = true,
                UserSrp = true
            }
        });

        // 4. Lambda Functions (.NET 8)
        var lambdaEnv = new Dictionary<string, string>
        {
            { "DATA_TABLE_NAME", dataTable.TableName },
            { "CONNECTIONS_TABLE_NAME", connectionsTable.TableName }
        };

        var backendAssetPath = ResolveBackendAssetPath();

        var restLambda = new Function(this, "StudyRoomRestLambda", new FunctionProps
        {
            FunctionName = "StudyRoom-RestApi",
            Runtime = Runtime.DOTNET_8,
            Handler = "StudyRoom.Backend::StudyRoom.Backend.Handlers.GroupHandlers::HandleAsync",
            Code = Code.FromAsset(backendAssetPath),
            Timeout = Duration.Seconds(30),
            MemorySize = 512,
            Environment = lambdaEnv
        });

        var webSocketLambda = new Function(this, "StudyRoomWebSocketLambda", new FunctionProps
        {
            FunctionName = "StudyRoom-WebSocketHub",
            Runtime = Runtime.DOTNET_8,
            Handler = "StudyRoom.Backend::StudyRoom.Backend.Handlers.WebSocketHandlers::HandleAsync",
            Code = Code.FromAsset(backendAssetPath),
            Timeout = Duration.Seconds(30),
            MemorySize = 512,
            Environment = lambdaEnv
        });

        // Least Privilege IAM permissions for Lambdas
        dataTable.GrantReadWriteData(restLambda);
        connectionsTable.GrantReadWriteData(webSocketLambda);
        dataTable.GrantReadWriteData(webSocketLambda);

        // 5. API Gateway HTTP API (REST endpoints)
        var httpApi = new CfnApi(this, "StudyRoomHttpApi", new CfnApiProps
        {
            Name = "StudyRoomHttpApi",
            ProtocolType = "HTTP"
        });

        var httpIntegration = new CfnIntegration(this, "StudyRoomHttpIntegration", new CfnIntegrationProps
        {
            ApiId = httpApi.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = restLambda.FunctionArn,
            PayloadFormatVersion = "2.0"
        });

        string[] httpRoutes = ["POST /groups", "GET /groups/{id}", "POST /groups/{id}/members", "$default"];
        foreach (var route in httpRoutes)
        {
            var routeId = route.Replace(" ", "_").Replace("/", "_").Replace("{", "").Replace("}", "").Replace("$", "Default_");
            new CfnRoute(this, $"HttpRoute_{routeId}", new CfnRouteProps
            {
                ApiId = httpApi.Ref,
                RouteKey = route,
                Target = $"integrations/{httpIntegration.Ref}"
            });
        }

        new CfnStage(this, "StudyRoomHttpStage", new CfnStageProps
        {
            ApiId = httpApi.Ref,
            StageName = "$default",
            AutoDeploy = true
        });

        restLambda.AddPermission("HttpApiInvokePermission", new Amazon.CDK.AWS.Lambda.Permission
        {
            Principal = new ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{httpApi.Ref}/*"
        });

        // 6. API Gateway WebSocket API (Real-time Broadcast & Relay)
        var webSocketApi = new CfnApi(this, "StudyRoomWebSocketApi", new CfnApiProps
        {
            Name = "StudyRoomWebSocketApi",
            ProtocolType = "WEBSOCKET",
            RouteSelectionExpression = "$request.body.action"
        });

        var webSocketIntegration = new CfnIntegration(this, "StudyRoomWebSocketIntegration", new CfnIntegrationProps
        {
            ApiId = webSocketApi.Ref,
            IntegrationType = "AWS_PROXY",
            IntegrationUri = $"arn:aws:apigateway:{this.Region}:lambda:path/2015-03-31/functions/{webSocketLambda.FunctionArn}/invocations"
        });

        string[] wsRoutes =
        [
            "$connect",
            "$disconnect",
            "$default",
            "JOIN_ROOM",
            "START_POMODORO",
            "PAUSE_POMODORO",
            "PASS_BATON",
            "MOVE_KANBAN_CARD",
            "SEND_CHAT",
            "TRIGGER_FLASHCARD"
        ];

        foreach (var route in wsRoutes)
        {
            var routeId = route.Replace("$", "Default_");
            new CfnRoute(this, $"WebSocketRoute_{routeId}", new CfnRouteProps
            {
                ApiId = webSocketApi.Ref,
                RouteKey = route,
                Target = $"integrations/{webSocketIntegration.Ref}"
            });
        }

        new CfnStage(this, "StudyRoomWebSocketStage", new CfnStageProps
        {
            ApiId = webSocketApi.Ref,
            StageName = "prod",
            AutoDeploy = true
        });

        webSocketLambda.AddPermission("WebSocketApiInvokePermission", new Amazon.CDK.AWS.Lambda.Permission
        {
            Principal = new ServicePrincipal("apigateway.amazonaws.com"),
            SourceArn = $"arn:aws:execute-api:{this.Region}:{this.Account}:{webSocketApi.Ref}/*"
        });

        // Least privilege permission to broadcast / manage connections on WebSocket API
        webSocketLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions = ["execute-api:ManageConnections"],
            Resources = [$"arn:aws:execute-api:{this.Region}:{this.Account}:{webSocketApi.Ref}/*"]
        }));

        // 7. Stack Outputs
        new CfnOutput(this, "UserPoolId", new CfnOutputProps { Value = userPool.UserPoolId });
        new CfnOutput(this, "UserPoolClientId", new CfnOutputProps { Value = appClient.UserPoolClientId });
        new CfnOutput(this, "HttpApiEndpoint", new CfnOutputProps { Value = $"https://{httpApi.Ref}.execute-api.{this.Region}.amazonaws.com" });
        new CfnOutput(this, "WebSocketApiEndpoint", new CfnOutputProps { Value = $"wss://{webSocketApi.Ref}.execute-api.{this.Region}.amazonaws.com/prod" });
    }

    private static string ResolveBackendAssetPath()
    {
        string[] candidates =
        [
            System.IO.Path.GetFullPath("backend/publish"),
            System.IO.Path.GetFullPath("../backend/publish"),
            System.IO.Path.GetFullPath("../../backend/publish"),
            System.IO.Path.GetFullPath("backend/src/StudyRoom.Backend"),
            System.IO.Path.GetFullPath("../backend/src/StudyRoom.Backend"),
            System.IO.Path.GetFullPath("../../backend/src/StudyRoom.Backend")
        ];

        foreach (var candidate in candidates)
        {
            if (System.IO.Directory.Exists(candidate) &&
                (System.IO.File.Exists(System.IO.Path.Combine(candidate, "StudyRoom.Backend.dll")) ||
                 System.IO.File.Exists(System.IO.Path.Combine(candidate, "StudyRoom.Backend.csproj"))))
            {
                return candidate;
            }
        }

        return System.IO.Path.GetFullPath("backend/src/StudyRoom.Backend");
    }
}
