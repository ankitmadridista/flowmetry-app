namespace Flowmetry.Infrastructure.Security;

public class SecurityObject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public bool IsEnabled { get; set; }

    public SecurityObject? Parent { get; set; }
}
