using TriggerDetective.Application.DTOs.ProductScan;

namespace TriggerDetective.Application.Interfaces;

public interface IProductScannerService
{
    Task<ProductScanResultDto> ScanProductLabelAsync(Guid userId, Stream labelPhoto, string locale = "en");
}
