using System;
using System.IO;
using System.Linq;
using LogisticsApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace LogisticsApp.Services;

public class LabelPrintService
{
    public LabelPrintService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void GenerateLabelsPdf(Waybill waybill, string filePath)
    {
        Document.Create(container =>
        {
            foreach (var point in waybill.Points.OrderBy(p => p.SequenceNumber))
            {
                container.Page(page =>
                {
                    // ИСПРАВЛЕНИЕ: Явное указание пространства имен для Unit
                    page.Size(100, 150, QuestPDF.Infrastructure.Unit.Millimetre);
                    page.Margin(4, QuestPDF.Infrastructure.Unit.Millimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor(Colors.Black));

                    page.Content().Column(col =>
                    {
                        col.Item().Border(2).BorderColor(Colors.Black).Padding(4).Column(innerCol =>
                        {
                            innerCol.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("ТД БРЯНСКИЙ").FontSize(14).Bold().ExtraBlack();
                                    c.Item().Text("МЯСОКОМБИНАТ").FontSize(14).Bold().ExtraBlack();
                                });
                                row.ConstantItem(40).AlignRight().Text($"#{point.SequenceNumber}").FontSize(28).Bold();
                            });

                            innerCol.Item().PaddingTop(5).LineHorizontal(2).LineColor(Colors.Black);
                            innerCol.Item().PaddingTop(10).AlignCenter().Text($"ЗАКАЗ № {point.OrderID}").FontSize(22).Bold();
                            innerCol.Item().PaddingTop(10).Text("КЛИЕНТ:").FontSize(9);
                            innerCol.Item().Text(point.Order?.Customer?.Name ?? "").FontSize(16).Bold();
                            innerCol.Item().PaddingTop(5).Text("АДРЕС ДОСТАВКИ:").FontSize(9);
                            innerCol.Item().Text(point.Order?.Customer?.Address ?? "").FontSize(12).Bold();

                            innerCol.Item().PaddingTop(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("ВЕС ГРУЗА:").FontSize(9);
                                    c.Item().Text($"{point.Order?.WeightKG} КГ").FontSize(18).Bold();
                                });
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("ТЕМП. РЕЖИМ:").FontSize(9);
                                    c.Item().Text(point.Order?.RequiredTempMode == true ? "РЕФРИЖЕРАТОР" : "ОБЫЧНЫЙ").FontSize(14).Bold();
                                });
                            });

                            innerCol.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Black);

                            innerCol.Item().PaddingTop(10).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("ТРАНСПОРТ:").FontSize(9);
                                    c.Item().Text(waybill.Vehicle?.RegNumber ?? "").FontSize(14).Bold();
                                    c.Item().PaddingTop(5).Text("ВОДИТЕЛЬ:").FontSize(9);
                                    c.Item().Text(waybill.Driver?.FullName ?? "").FontSize(11).Bold();
                                    c.Item().PaddingTop(5).Text("ДАТА ОТГРУЗКИ:").FontSize(9);
                                    c.Item().Text(waybill.DateCreate.ToString("dd.MM.yyyy HH:mm")).FontSize(11).Bold();
                                });

                                string qrPayload = $"ЗАКАЗ №{point.OrderID}\nПокупатель: {point.Order?.Customer?.Name}\nАдрес: {point.Order?.Customer?.Address}\nВес: {point.Order?.WeightKG} кг\nРейс: {waybill.WaybillID}\nТС: {waybill.Vehicle?.RegNumber}";
                                byte[] qrCodeImage = GenerateQrCode(qrPayload);

                                row.ConstantItem(85).AlignRight().Image(qrCodeImage);
                            });
                        });
                    });
                });
            }
        }).GeneratePdf(filePath);
    }

    private byte[] GenerateQrCode(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(20);
    }
}