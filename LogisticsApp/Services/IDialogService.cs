using LogisticsApp.Models;

namespace LogisticsApp.Services;

public interface IDialogService
{
    bool ShowConfirmation(string title, string message);
    void ShowError(string title, string message);
    void ShowWarning(string title, string message);
    void ShowMessageBox(string title, string message);
    bool ShowCustomerEditor(Customer? customer);
    bool ShowDriverEditor(Driver? driver);
    bool ShowVehicleEditor(Vehicle? vehicle);
    bool ShowOrderEditor(Order? order);
    bool ShowWaybillEditor(Waybill? waybill);
    bool ShowUserEditor(out string? newPasswordHash, User? user = null);
    bool ShowVehicleServiceRecordEditor(out VehicleServiceRecord? updatedRecord, VehicleServiceRecord? record = null, int currentOdometer = 0);
    bool ShowProductGroupEditor(out ProductGroup? group, ProductGroup? existingGroup = null);
    bool ShowUnitEditor(out Unit? unit, Unit? existingUnit = null);
    bool ShowProductEditor(int? productId = null);
    bool ShowNomenclaturePicker(out int? selectedProductId);
    bool ShowInventoryDocumentEditor(InventoryDocument? document);
    bool ShowPaymentDocumentEditor(MutualSettlement? settlement);
}