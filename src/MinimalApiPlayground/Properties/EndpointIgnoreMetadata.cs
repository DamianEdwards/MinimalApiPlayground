namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Defines a contract for metadata that indicates an endpoint should be ignored by endpoint metadata consumers.
/// </summary>
public interface IEndpointIgnoreMetadata
{

}

/// <summary>
/// Metadata that indicates an endpoint should be ignored by endpoint metadata consumers.
/// </summary>
public class EndpointIgnoreMetadata : IEndpointIgnoreMetadata
{

}