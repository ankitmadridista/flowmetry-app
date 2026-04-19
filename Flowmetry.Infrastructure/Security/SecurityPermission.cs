namespace Flowmetry.Infrastructure.Security;

public class SecurityPermission
{
    public int Id { get; set; }
    public int ObjId { get; set; }
    public int OperationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
