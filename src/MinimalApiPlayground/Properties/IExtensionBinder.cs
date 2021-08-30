namespace MinimalApiPlayground.ModelBinding
{
    using System.Reflection;

    /// <summary>
    /// Defines the signature of the static method RequestDelegateFactory looks for when performing custom
    /// parameter binding. Use this interface along with EnablePreviewFeatures in the project file and a
    /// reference to the System.Runtime.Experimental package to get compile-time checking of BindAsync
    /// implementations.
    /// </summary>
    /// <typeparam name="TSelf">The type the interface is implemented on.</typeparam>
    public interface IExtensionBinder<TSelf> where TSelf : IExtensionBinder<TSelf>
    {
        /// <summary>
        /// The method discovered by RequestDelegateFactory on types used as parameters of route
        /// handler delegates to support custom binding.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/>.</param>
        /// <param name="parameter">The <see cref="ParameterInfo"/> for the parameter being bound to.</param>
        /// <returns>The value to assign to the parameter.</returns>
        static abstract ValueTask<TSelf?> BindAsync(HttpContext context, ParameterInfo parameter);
    }
}
