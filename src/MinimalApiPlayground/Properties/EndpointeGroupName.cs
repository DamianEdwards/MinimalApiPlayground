namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Declares the group name for this endpoint method or delegate.<br />
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate, Inherited = false, AllowMultiple = false)]
public sealed class EndpointGroupNameAttribute: Attribute, IEndpointGroupNameMetadata
{
    public EndpointGroupNameAttribute(string endpointGroupName)
    {
        if (endpointGroupName == null)
        {
            throw new ArgumentNullException(nameof(endpointGroupName));
        }

        EndpointGroupName = endpointGroupName;
    }

    /// <summary>
    /// The endpoint group name.
    /// </summary>
    public string EndpointGroupName { get; }
}

/// <summary>
/// Specifies an endpoint group name in Microsoft.AspNetCore.Http.Endpoint.Metadata.
/// </summary>
public class EndpointGroupNameMetadata : IEndpointGroupNameMetadata
{
    public EndpointGroupNameMetadata(string endpointGroupName)
    {
        if (endpointGroupName == null)
        {
            throw new ArgumentNullException(nameof(endpointGroupName));
        }

        EndpointGroupName = endpointGroupName;
    }

    /// <summary>
    /// The endpoint group name.
    /// </summary>
    public string EndpointGroupName { get; }
}

/// <summary>
/// Defines a contract use to specify an endpoint group name in Microsoft.AspNetCore.Http.Endpoint.Metadata.
/// </summary>
public interface IEndpointGroupNameMetadata
{
    /// <summary>
    /// The endpoint group name.
    /// </summary>
    string EndpointGroupName { get; }
}