using System;
using System.Data;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LogisticsApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QDocument = QuestPDF.Fluent.Document;
using WDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;

namespace LogisticsApp.Services;

public class ReportService
{
    public ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void GenerateWaybillPdf(Waybill waybill, string filePath)
    {
        QDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                // ИСПРАВЛЕНИЕ: Явное указание пространства имен для Unit
                page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header().Text($"Путевой лист №{waybill.WaybillID}").SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Дата создания: {waybill.DateCreate:dd.MM.yyyy HH:mm}");
                    col.Item().Text($"Автомобиль: {waybill.Vehicle?.RegNumber} ({waybill.Vehicle?.Model})");
                    col.Item().Text($"Водитель: {waybill.Driver?.FullName}");
                    col.Item().Text($"Статус: {waybill.Status}");

                    col.Item().PaddingTop(10).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(30);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.ConstantColumn(50);
                        });

                        t.Header(h =>
                        {
                            h.Cell().BorderBottom(1).Padding(5).Text("№");
                            h.Cell().BorderBottom(1).Padding(5).Text("Заказ");
                            h.Cell().BorderBottom(1).Padding(5).Text("Адрес");
                            h.Cell().BorderBottom(1).Padding(5).Text("Вес");
                        });

                        if (waybill.Points != null)
                        {
                            foreach (var p in waybill.Points.OrderBy(x => x.SequenceNumber))
                            {
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(p.SequenceNumber.ToString());
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(p.OrderID.ToString());
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(p.Order?.Customer?.Address ?? "");
                                t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(p.Order?.WeightKG.ToString() ?? "");
                            }
                        }
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    public void GenerateWaybillExcel(Waybill waybill, string filePath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Путевой лист");

        ws.Cell("A1").Value = $"Путевой лист №{waybill.WaybillID}";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;

        ws.Cell("A3").Value = "Автомобиль:";
        ws.Cell("B3").Value = $"{waybill.Vehicle?.RegNumber} ({waybill.Vehicle?.Model})";

        ws.Cell("A4").Value = "Водитель:";
        ws.Cell("B4").Value = waybill.Driver?.FullName;

        ws.Cell("A6").Value = "№ п/п";
        ws.Cell("B6").Value = "Заказ";
        ws.Cell("C6").Value = "Клиент";
        ws.Cell("D6").Value = "Адрес";
        ws.Cell("E6").Value = "Вес (кг)";
        ws.Range("A6:E6").Style.Font.Bold = true;

        int row = 7;
        if (waybill.Points != null)
        {
            foreach (var p in waybill.Points.OrderBy(x => x.SequenceNumber))
            {
                ws.Cell(row, 1).Value = p.SequenceNumber;
                ws.Cell(row, 2).Value = p.OrderID;
                ws.Cell(row, 3).Value = p.Order?.Customer?.Name;
                ws.Cell(row, 4).Value = p.Order?.Customer?.Address;
                ws.Cell(row, 5).Value = p.Order?.WeightKG;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public void ExportDataTableToPdf(DataTable table, string title, string grandTotal, string filePath)
    {
        QDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                // ИСПРАВЛЕНИЕ: Явное указание пространства имен для Unit
                page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().PaddingBottom(10).Column(col =>
                {
                    col.Item().Text("ООО «ТД Брянский мясокомбинат»").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                    col.Item().PaddingTop(5).Text(title).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Text($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Content().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        for (int i = 0; i < table.Columns.Count; i++)
                            c.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        foreach (DataColumn col in table.Columns)
                        {
                            h.Cell().Background(Colors.Grey.Lighten3)
                             .BorderBottom(1).BorderColor(Colors.Black)
                             .Padding(5).Text(col.ColumnName).Bold().FontSize(11);
                        }
                    });

                    foreach (DataRow row in table.Rows)
                    {
                        foreach (var item in row.ItemArray)
                        {
                            string val = item is DateTime dt ? dt.ToString("dd.MM.yyyy") : item?.ToString() ?? "";
                            t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4)
                             .Padding(5).Text(val);
                        }
                    }

                    if (!string.IsNullOrEmpty(grandTotal))
                    {
                        t.Footer(f =>
                        {
                            f.Cell().ColumnSpan((uint)table.Columns.Count)
                             .Background(Colors.Yellow.Lighten4)
                             .BorderTop(2).BorderColor(Colors.Black)
                             .AlignRight().Padding(5)
                             .Text($"ИТОГО: {grandTotal}").Bold().FontSize(12).FontColor(Colors.Red.Darken2);
                        });
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Страница ");
                    x.CurrentPageNumber();
                    x.Span(" из ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    public void ExportDataTableToExcel(DataTable table, string title, string grandTotal, string filePath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Сводный Отчет");

        ws.Cell("A1").Value = "ООО «ТД Брянский мясокомбинат»";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A2").Value = title;
        ws.Cell("A2").Style.Font.Bold = true;
        ws.Cell("A2").Style.Font.FontSize = 14;
        ws.Cell("A3").Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
        ws.Cell("A3").Style.Font.Italic = true;

        int row = 5;
        int col = 1;

        foreach (DataColumn column in table.Columns)
        {
            var cell = ws.Cell(row, col);
            cell.Value = column.ColumnName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            col++;
        }
        row++;

        foreach (DataRow dr in table.Rows)
        {
            col = 1;
            foreach (var item in dr.ItemArray)
            {
                var cell = ws.Cell(row, col);
                if (item is DateTime dt) cell.Value = dt.ToShortDateString();
                else if (item is double d) cell.Value = d;
                else if (item is int i) cell.Value = i;
                else cell.Value = item?.ToString();

                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                col++;
            }
            row++;
        }

        if (!string.IsNullOrEmpty(grandTotal))
        {
            var totalCell = ws.Cell(row, table.Columns.Count);
            totalCell.Value = $"ИТОГО: {grandTotal}";
            totalCell.Style.Font.Bold = true;
            totalCell.Style.Font.FontColor = XLColor.DarkRed;
            totalCell.Style.Fill.BackgroundColor = XLColor.LemonChiffon;
            totalCell.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            totalCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public void ExportDataTableToWord(DataTable table, string title, string grandTotal, string filePath)
    {
        using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new WDocument();
        var body = mainPart.Document.AppendChild(new Body());

        body.AppendChild(new Paragraph(new Run(new Text("ООО «ТД Брянский мясокомбинат»")) { RunProperties = new RunProperties(new Bold()) }));

        var titlePara = body.AppendChild(new Paragraph(new Run(new Text(title)) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" }) }));
        titlePara.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });

        body.AppendChild(new Paragraph(new Run(new Text(""))));

        var wTable = new Table();
        var tblProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 6 },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
            ),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
        );
        wTable.AppendChild(tblProps);

        var trHeader = new TableRow();
        foreach (DataColumn col in table.Columns)
        {
            var tc = new TableCell(new Paragraph(new Run(new Text(col.ColumnName)) { RunProperties = new RunProperties(new Bold()) }));
            tc.Append(new TableCellProperties(new Shading { Fill = "D3D3D3" }));
            trHeader.Append(tc);
        }
        wTable.Append(trHeader);

        foreach (DataRow row in table.Rows)
        {
            var tr = new TableRow();
            foreach (var item in row.ItemArray)
            {
                string val = item is DateTime dt ? dt.ToString("dd.MM.yyyy") : item?.ToString() ?? "";
                tr.Append(new TableCell(new Paragraph(new Run(new Text(val)))));
            }
            wTable.Append(tr);
        }
        body.Append(wTable);

        if (!string.IsNullOrEmpty(grandTotal))
        {
            body.AppendChild(new Paragraph(new Run(new Text(""))));
            var totalPara = body.AppendChild(new Paragraph(new Run(new Text($"ИТОГО: {grandTotal}")) { RunProperties = new RunProperties(new Bold(), new FontSize { Val = "24" }) }));
            totalPara.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Right });
        }

        mainPart.Document.Save();
    }
}