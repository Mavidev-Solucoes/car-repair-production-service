namespace Domain.Entities;

public sealed class ServiceJob
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 500;

    private ServiceJob()
    {
    }

    public ServiceJob(Guid id, string name, string? description)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Service job identifier is required.", nameof(id));
        }

        var normalizedName = NormalizeRequiredText(name, NameMaxLength, nameof(name));
        var normalizedDescription = NormalizeOptionalText(description, DescriptionMaxLength, nameof(description));

        Id = id;
        Name = normalizedName;
        Description = normalizedDescription;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    private static string NormalizeRequiredText(string value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", paramName);
        }

        return normalizedValue;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            return null;
        }

        if (normalizedValue.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", paramName);
        }

        return normalizedValue;
    }
}
