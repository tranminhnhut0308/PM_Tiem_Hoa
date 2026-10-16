namespace ShopHoa.Models;

public sealed class ModulePermission
{
    public string Module { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public bool HasView { get; set; }
    public bool HasCreate { get; set; }
    public bool HasEdit { get; set; }
    public bool HasDelete { get; set; }
    public bool HasImport { get; set; }
    public bool HasExport { get; set; }
    public bool HasCancel { get; set; }
    public bool HasLock { get; set; }
    public bool HasUnlock { get; set; }
    public bool HasResetPassword { get; set; }
    public bool HasAssignPermission { get; set; }
    public bool HasCreateReceipt { get; set; }
    public bool HasCreatePayment { get; set; }
    public bool HasManageRoles { get; set; }
}
