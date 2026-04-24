using System.Threading.Tasks;
using LogisticsApp.Models.DTOs.Reports;

namespace LogisticsApp.Services.Interfaces;

public interface IWaybillDocumentService
{
    Task<byte[]> GenerateRouteManifestExcelAsync(RouteManifestDto manifestData);
    Task<byte[]> GenerateDriverDocumentPdfAsync(DriverDocumentDto documentData);
}