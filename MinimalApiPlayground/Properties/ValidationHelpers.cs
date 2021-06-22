static class ValidationHelpers
{
    public static bool TryValidate<T>(T target, out IDictionary<string, string[]> errors) where T : class
    {
        // TODO: Make recursive

        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var validationContext = new ValidationContext(target);
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

        errors = new Dictionary<string, string[]>(errorsList.Count);
        foreach (var error in errorsList)
        {
            errors.Add(error.Key, error.Value.ToArray());
        }

        return isValid;
    }
}