namespace Flowmetry.Domain;

public class Customer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public int RiskScore { get; private set; }

    // Required by EF Core
    private Customer() { }
}
