using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace LogisticsApp.Models.DTOs.Reports;

public class WaybillRegistryDto
{
    [DisplayName("№ П/Л")]
    public int WaybillID { get; set; }

    [DisplayName("Дата создания")]
    public DateTime DateCreate { get; set; }

    [DisplayName("Водитель")]
    public string DriverName { get; set; } = string.Empty;

    [DisplayName("Транспортное средство")]
    public string VehicleReg { get; set; } = string.Empty;

    [DisplayName("Статус")]
    public string Status { get; set; } = string.Empty;

    [DisplayName("Дистанция (км)")]
    public double Distance { get; set; }

    [DisplayName("Точек маршрута")]
    public int PointsCount { get; set; }

    [DisplayName("Вес груза (кг)")]
    public double TotalWeight { get; set; }
}

public class DriverKpiDto
{
    [DisplayName("ФИО Водителя")]
    public string DriverName { get; set; } = string.Empty;

    [DisplayName("Категория прав")]
    public string Category { get; set; } = string.Empty;

    [DisplayName("Выполнено рейсов")]
    public int TotalTrips { get; set; }

    [DisplayName("Общий пробег (км)")]
    public double TotalDistance { get; set; }

    [DisplayName("Перевезено груза (кг)")]
    public double TotalWeightTransported { get; set; }

    [DisplayName("Успешных точек")]
    public int SuccessPoints { get; set; }
}

public class CustomerAnalyticsDto
{
    [DisplayName("Контрагент")]
    public string CustomerName { get; set; } = string.Empty;

    [DisplayName("Тип контрагента")]
    public string CustomerType { get; set; } = string.Empty;

    [DisplayName("Кол-во заказов")]
    public int OrdersCount { get; set; }

    [DisplayName("Общий вес (кг)")]
    public double TotalWeight { get; set; }

    [DisplayName("Реф. заказов")]
    public int RefOrdersCount { get; set; }
}

public class InventoryBalanceDto
{
    [DisplayName("Склад")]
    public string WarehouseName { get; set; } = string.Empty;

    [DisplayName("Артикул")]
    public string SKU { get; set; } = string.Empty;

    [DisplayName("Номенклатура")]
    public string ProductName { get; set; } = string.Empty;

    [DisplayName("Нач. остаток (баз. ед.)")]
    public int InitialBalance { get; set; }

    [DisplayName("Приход (баз. ед.)")]
    public int ReceiptQuantity { get; set; }

    [DisplayName("Расход (баз. ед.)")]
    public int ExpenseQuantity { get; set; }

    [DisplayName("Кон. остаток (баз. ед.)")]
    public int FinalBalance { get; set; }
}

public class DeficitAnalysisDto
{
    [DisplayName("Артикул")]
    public string SKU { get; set; } = string.Empty;

    [DisplayName("Номенклатура")]
    public string ProductName { get; set; } = string.Empty;

    [DisplayName("Требуется по заказам")]
    public int RequiredQuantity { get; set; }

    [DisplayName("Доступно на складах")]
    public int AvailableQuantity { get; set; }

    [DisplayName("Дефицит (нехватка)")]
    public int DeficitQuantity { get; set; }
}

// --- НОВЫЕ DTO ДЛЯ ФИНАНСОВ ---

public class CustomerReconciliationDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string INN { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public List<ReconciliationItemDto> Items { get; set; } = new();
    public decimal FinalBalance { get; set; }
}

public class ReconciliationItemDto
{
    [DisplayName("Дата операции")]
    public DateTime Date { get; set; }

    [DisplayName("Документ / Основание")]
    public string Document { get; set; } = string.Empty;

    [DisplayName("Увеличение долга (Отгрузка)")]
    public decimal DebtIncrease { get; set; }

    [DisplayName("Оплата (Приход)")]
    public decimal DebtDecrease { get; set; }
}

public class CashFlowReportDto
{
    [DisplayName("Дата платежа")]
    public DateTime PaymentDate { get; set; }

    [DisplayName("Плательщик (Контрагент)")]
    public string CustomerName { get; set; } = string.Empty;

    [DisplayName("Сумма (₽)")]
    public decimal Amount { get; set; }

    [DisplayName("Назначение платежа")]
    public string Description { get; set; } = string.Empty;
}