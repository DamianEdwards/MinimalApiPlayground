using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

static class ValidationHelpers
{
    public static bool TryValidate<T>(T target, out IDictionary<string, string[]> errors) where T : class
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        errors = new Dictionary<string, string[]>();
        var isValid = TryValidateImpl(target, errors);

        return isValid;
    }

    private static int _maxDepth = 3;
    private static ConcurrentDictionary<Type, PropertyInfo[]> _typeCache = new();

    private static bool TryValidateImpl(object target, IDictionary<string, string[]> errors, string prefix = "", int currentDepth = 0)
    {
        // TODO: Add cycle detection

        var validationContext = new ValidationContext(target);
        var validationResults = new List<ValidationResult>();

        // Validate the simple properties on the target first (Validator.TryValidateObject is non-recursive)
        var isValid = Validator.TryValidateObject(target, validationContext, validationResults);

        var errorsList = new Dictionary<string, List<string>>();
        foreach (var result in validationResults)
        {
            foreach (var name in result.MemberNames)
            {
                List<string> fieldErrors;
                if (errorsList.ContainsKey(name))
                {
                    fieldErrors = errorsList[name];
                }
                else
                {
                    fieldErrors = new List<string>();
                    errorsList.Add(name, fieldErrors);
                }
                fieldErrors.Add(result.ErrorMessage);
            }
        }

        foreach (var error in errorsList)
        {
            errors.Add($"{prefix}{error.Key}", error.Value.ToArray());
        }

        if (isValid && currentDepth < _maxDepth)
        {
            // Validate complex properties
            var complexProperties = _typeCache.GetOrAdd(target.GetType(),t =>
                t.GetProperties().Where(p => IsComplexType(p.PropertyType)).ToArray());
            
            foreach (var property in complexProperties)
            {
                var propertyName = property.Name;
                var propertyType = property.PropertyType;

                if (propertyType.IsAssignableTo(typeof(IEnumerable)))
                {
                    // Validate each instance in the collection
                    var items = property.GetValue(target) as IEnumerable;
                    var index = 0;
                    foreach (var item in items)
                    {
                        var itemPrefix = $"{propertyName}[{index}].";
                        isValid = TryValidateImpl(item, errors, prefix: itemPrefix, currentDepth + 1);

                        if (!isValid)
                        {
                            break;
                        }
                        index++;
                    }
                }
                else
                {
                    var propertyValue = property.GetValue(target);
                    isValid = TryValidateImpl(propertyValue, errors, prefix: $"{propertyName}.", currentDepth + 1);

                    if (!isValid)
                    {
                        break;
                    }
                }
            }
        }

        return isValid;
    }

    private static bool IsComplexType(Type type)
    {
        if (type.IsGenericType && (
            type.GetGenericTypeDefinition() == typeof(Nullable<>)
            || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            || type.GetGenericTypeDefinition() == typeof(ICollection<>)
            || type.GetGenericTypeDefinition() == typeof(IList<>)
            ))
        {
            // Known wrapper type, check if the nested type is complex
            return IsComplexType(type.GetGenericArguments()[0]);
        }

        return !(type.IsPrimitive
            || type.IsEnum
            || type.Equals(typeof(string))
            || type.Equals(typeof(decimal)));
    }
}