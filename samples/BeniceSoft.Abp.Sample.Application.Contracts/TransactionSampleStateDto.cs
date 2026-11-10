namespace BeniceSoft.Abp.Sample.Application.Contracts;

public class TransactionSampleStateDto
{
    public string BizId { get; set; } = string.Empty;
    public string? OrderState { get; set; }
    public string? InventoryState { get; set; }
    public int? OrderQuantity { get; set; }
    public int? AvailableQuantity { get; set; }
    public int? FrozenQuantity { get; set; }
    public int? DeductedQuantity { get; set; }
}
