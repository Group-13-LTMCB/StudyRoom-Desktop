using Amazon.ApiGatewayManagementApi;

namespace StudyRoom.Backend.Services;

public interface IApiGatewayManagementClientFactory
{
    IAmazonApiGatewayManagementApi CreateClient(string endpoint);
}

public sealed class DefaultApiGatewayManagementClientFactory : IApiGatewayManagementClientFactory
{
    public IAmazonApiGatewayManagementApi CreateClient(string endpoint)
    {
        var config = new AmazonApiGatewayManagementApiConfig
        {
            ServiceURL = endpoint
        };
        return new AmazonApiGatewayManagementApiClient(config);
    }
}
