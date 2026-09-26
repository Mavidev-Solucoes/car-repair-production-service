namespace Domain.Entities;

public sealed class ServiceJob
{
    private ServiceJob()
    {
    }

    public ServiceJob(Guid id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }
}
