namespace Microsoft.AspNetCore.Routing;

/// <summary>
/// Declares the name for this endpoint method or delegate.<br />
/// The name is used to lookup the endpoint during link generation and as an operationId when generating OpenAPI documentation.<br />
/// The name must be unique per endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate, Inherited = false, AllowMultiple = false)]
sealed class EndpointNameAttribute : Attribute, IEndpointNameMetadata
{
    /// <summary>
    /// Initializes an instance of the EndpointNameAttribute.
    /// </summary>
    /// <param name="endpointName">The endpoint name.</param>
    public EndpointNameAttribute(string endpointName)
    {
        if (endpointName == null)
        {
            throw new ArgumentNullException(nameof(endpointName));
        }

        EndpointName = endpointName;
    }

    /// <summary>
    /// The endpoint name.
    /// </summary>
    public string EndpointName { get; }
}