static class ValidationHelpers
{
    public static bool TryValidate<T>(this T target, out ValidationErrors errors) where T : class
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var items = new Dictionary<object, object>();
        var validationContext = new ValidationContext(target, items);
        var validationResults = new List<ValidationResult>();

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

        errors = new ValidationErrors(errorsList.Count);
        foreach (var error in errorsList)
        {
            errors.Add(error.Key, error.Value.ToArray());
        }

        return isValid;
    }
}

class ValidationErrors : Dictionary<string, string[]>
{
    public ValidationErrors() : base()
    {

    }

    public ValidationErrors(int capacity)
        : base(capacity)
    {

    }
}