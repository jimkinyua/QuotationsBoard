public class TenureDTO
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal Tenor { get; set; }
    public Boolean IsValidationEnabled { get; set; }
}