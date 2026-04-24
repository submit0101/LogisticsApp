using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services.Interfaces;

namespace LogisticsApp.Services.Implementations;

public class WaybillDocumentService : IWaybillDocumentService
{
    public WaybillDocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateRouteManifestExcelAsync(RouteManifestDto manifestData)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Перечень покупателей");

            worksheet.Cell(1, 1).Value = $"Перечень покупателей по ТТН № {manifestData.WaybillNumber} от {manifestData.DispatchDate:dd MMMM yyyy} г.";
            worksheet.Cell(2, 1).Value = $"Водитель: {manifestData.DriverFullName}";
            worksheet.Cell(3, 1).Value = $"№ авто: {manifestData.VehicleRegistrationNumber} ({manifestData.VehicleModel})";
            worksheet.Cell(4, 1).Value = $"Маршрут: {manifestData.RouteName}";

            var titleRange = worksheet.Range(1, 1, 4, 1);
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 12;

            worksheet.Cell(6, 1).Value = "Тара";
            worksheet.Range(6, 1, 6, 2).Merge();
            worksheet.Cell(7, 1).Value = "Кор.";
            worksheet.Cell(7, 2).Value = "Ящ.";

            worksheet.Cell(6, 3).Value = "№";
            worksheet.Range(6, 3, 7, 3).Merge();

            worksheet.Cell(6, 4).Value = "Покупатель";
            worksheet.Range(6, 4, 7, 4).Merge();

            worksheet.Cell(6, 5).Value = "Адрес";
            worksheet.Range(6, 5, 7, 5).Merge();

            worksheet.Cell(6, 6).Value = "Нетто, кг";
            worksheet.Range(6, 6, 7, 6).Merge();

            worksheet.Cell(6, 7).Value = "Брутто, кг";
            worksheet.Range(6, 7, 7, 7).Merge();

            var headerRange = worksheet.Range(6, 1, 7, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var currentRow = 8;
            decimal totalNetWeight = 0;
            decimal totalGrossWeight = 0;
            decimal totalBoxes = 0;
            decimal totalCrates = 0;

            foreach (var point in manifestData.Points)
            {
                worksheet.Cell(currentRow, 1).Value = point.BoxesCount;
                worksheet.Cell(currentRow, 2).Value = point.CratesCount;
                worksheet.Cell(currentRow, 3).Value = point.OrderNumber;
                worksheet.Cell(currentRow, 4).Value = point.CustomerName;
                worksheet.Cell(currentRow, 5).Value = point.CustomerAddress;
                worksheet.Cell(currentRow, 6).Value = point.NetWeight;
                worksheet.Cell(currentRow, 7).Value = point.GrossWeight;

                totalNetWeight += point.NetWeight;
                totalGrossWeight += point.GrossWeight;
                totalBoxes += point.BoxesCount;
                totalCrates += point.CratesCount;

                currentRow++;
            }

            if (manifestData.Points.Any())
            {
                var dataRange = worksheet.Range(8, 1, currentRow - 1, 7);
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Range(8, 5, currentRow - 1, 5).Style.Alignment.WrapText = true;
            }

            var totalsRow = worksheet.Range(currentRow, 1, currentRow, 7);
            totalsRow.Style.Font.Bold = true;
            totalsRow.Style.Fill.BackgroundColor = XLColor.WhiteSmoke;
            totalsRow.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            totalsRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            worksheet.Cell(currentRow, 1).Value = totalBoxes;
            worksheet.Cell(currentRow, 2).Value = totalCrates;
            worksheet.Cell(currentRow, 4).Value = "ИТОГО:";
            worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(currentRow, 6).Value = totalNetWeight;
            worksheet.Cell(currentRow, 7).Value = totalGrossWeight;

            worksheet.Column(1).Width = 6;
            worksheet.Column(2).Width = 6;
            worksheet.Column(3).Width = 10;
            worksheet.Column(4).Width = 35;
            worksheet.Column(5).Width = 45;
            worksheet.Column(6).Width = 12;
            worksheet.Column(7).Width = 12;

            using var memoryStream = new MemoryStream();
            workbook.SaveAs(memoryStream);
            return memoryStream.ToArray();
        });
    }

    public async Task<byte[]> GenerateDriverDocumentPdfAsync(DriverDocumentDto documentData)
    {
        return await Task.Run(() =>
        {
            var document = BuildDocument(documentData);
            return document.GeneratePdf();
        });
    }

    private Document BuildDocument(DriverDocumentDto documentData)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);

                page.DefaultTextStyle(style => style.FontFamily(Fonts.Arial).FontSize(10));

                page.Header().Element(headerContainer => ComposeHeader(headerContainer, documentData));
                page.Content().Element(contentContainer => ComposeContent(contentContainer, documentData));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    private void ComposeHeader(IContainer container, DriverDocumentDto documentData)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text("ДОКУМЕНТ ТОЛЬКО ДЛЯ ВОДИТЕЛЯ!!!")
                .FontColor(Colors.Red.Medium).Bold().FontSize(16);

            column.Item().PaddingTop(10).Text($"Товарно-транспортная накладная № {documentData.DocumentNumber}")
                .FontColor(Colors.Black).Bold().FontSize(14);

            column.Item().Text(documentData.DocumentDate.ToString("dd MMMM yyyy г.")).FontSize(12);
            column.Item().PaddingTop(5).Text($"Водитель: {documentData.DriverFullName}").Bold();
            column.Item().Text($"№ авто: {documentData.VehicleModel} {documentData.VehicleRegistrationNumber}").Bold();
            column.Item().Text($"Маршрут: {documentData.RouteName}");

            column.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem().Text($"Отпуск разрешил: Руководитель {documentData.ManagerName}");
                row.RelativeItem().Text($"Гл.бухгалтер {documentData.ChiefAccountantName}");
            });
        });
    }

    private void ComposeContent(IContainer container, DriverDocumentDto documentData)
    {
        container.PaddingVertical(15).Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(35);
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Red.Medium).Padding(5).Text("№").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Red.Medium).Padding(5).Text("Код").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Red.Medium).Padding(5).Text("Номенклатура").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Red.Medium).Padding(5).AlignRight().Text("Кол-во").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Red.Medium).Padding(5).AlignRight().Text("Вес, кг").FontColor(Colors.White).Bold();
                });

                foreach (var item in documentData.Items)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.SequenceNumber.ToString());
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.ProductCode);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(item.ProductName);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.Quantity.ToString("F2"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).AlignRight().Text(item.Weight.ToString("F2"));
                }
            });

            var totalWeight = documentData.Items.Sum(item => item.Weight);
            var totalQuantity = documentData.Items.Sum(item => item.Quantity);

            column.Item().PaddingTop(10).AlignRight().Text($"ИТОГО: {totalWeight:F2} кг")
                .FontSize(14).FontColor(Colors.Red.Medium).Bold();

            column.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Text("Сдал ___________________________");
                row.RelativeItem().AlignRight().Text("Груз к перевозке принял ___________________________");
            });

            column.Item().PaddingTop(20).Text("Товар принял полностью, претензий не имею ________________________________").Bold();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Страница ");
            text.CurrentPageNumber();
            text.Span(" из ");
            text.TotalPages();
        });
    }
}