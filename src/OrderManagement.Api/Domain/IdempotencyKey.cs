namespace OrderManagement.Api.Domain;

public class IdempotencyKey
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
