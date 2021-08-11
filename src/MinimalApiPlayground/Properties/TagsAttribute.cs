
namespace Microsoft.AspNetCore.Http;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Delegate)]
public class TagsAttribute : Attribute, ITagsMetadata
{
    public TagsAttribute(params string[] tags)
    {
        Tags = new List<string>(tags);
    }

    public IList<string> Tags { get; }
}

public interface ITagsMetadata
{
    IList<string> Tags { get; }
}
