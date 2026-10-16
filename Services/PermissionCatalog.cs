using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Danh mục nhóm quyền + thứ tự hiển thị + key "xem" của từng module.
/// Nguồn quyền duy nhất cho UI phân quyền, menu và các lớp kiểm tra quyền.
/// <para>
/// Mỗi phần tử trong <see cref="Groups"/> là một MENU; các key con là từng CHỨC NĂNG.
/// UI phân quyền dựng cây cha/con trực tiếp từ đây.
/// </para>
/// </summary>
public static class PermissionCatalog
{
    public sealed record GroupDefinition(string Nhom, string TenNhom, string[] Keys);

    public static readonly IReadOnlyList<GroupDefinition> Groups = new[]
    {
        new GroupDefinition("Dashboard", "Tổng quan", new[]
        {
            PermissionKeys.DashboardView
        }),
        new GroupDefinition("Sales", "Bán hàng", new[]
        {
            PermissionKeys.SalesView,
            PermissionKeys.SalesSearchProduct,
            PermissionKeys.SalesAddProduct,
            PermissionKeys.SalesEditQuantity,
            PermissionKeys.SalesRemoveProduct,
            PermissionKeys.SalesDiscount,
            PermissionKeys.SalesPayment,
            PermissionKeys.SalesPrint,
            PermissionKeys.SalesCancel,
            PermissionKeys.SalesHistory
        }),
        new GroupDefinition("Products", "Hàng hóa", new[]
        {
            PermissionKeys.ProductsView,
            PermissionKeys.ProductsDetail,
            PermissionKeys.ProductsCreate,
            PermissionKeys.ProductsEdit,
            PermissionKeys.ProductsDelete,
            PermissionKeys.ProductsImport,
            PermissionKeys.ProductsExport,
            PermissionKeys.ProductsAdjustStock,
            PermissionKeys.ProductsUpdatePrice,
            PermissionKeys.ProductsUpdateCost
        }),
        new GroupDefinition("Invoices", "Hóa đơn", new[]
        {
            PermissionKeys.InvoicesView,
            PermissionKeys.InvoicesDetail,
            PermissionKeys.InvoicesCreate,
            PermissionKeys.InvoicesEdit,
            PermissionKeys.InvoicesCancel,
            PermissionKeys.InvoicesDelete,
            PermissionKeys.InvoicesPrint,
            PermissionKeys.InvoicesImport,
            PermissionKeys.InvoicesExport
        }),
        new GroupDefinition("Customers", "Khách hàng", new[]
        {
            PermissionKeys.CustomersView,
            PermissionKeys.CustomersDetail,
            PermissionKeys.CustomersCreate,
            PermissionKeys.CustomersEdit,
            PermissionKeys.CustomersDelete,
            PermissionKeys.CustomersImport,
            PermissionKeys.CustomersExport,
            PermissionKeys.CustomersDebtView,
            PermissionKeys.CustomersDebtPay
        }),
        new GroupDefinition("Suppliers", "Nhà cung cấp", new[]
        {
            PermissionKeys.SuppliersView,
            PermissionKeys.SuppliersDetail,
            PermissionKeys.SuppliersCreate,
            PermissionKeys.SuppliersEdit,
            PermissionKeys.SuppliersDelete,
            PermissionKeys.SuppliersImport,
            PermissionKeys.SuppliersExport,
            PermissionKeys.SuppliersDebtView,
            PermissionKeys.SuppliersDebtPay
        }),
        new GroupDefinition("Purchases", "Phiếu nhập hàng", new[]
        {
            PermissionKeys.PurchasesView,
            PermissionKeys.PurchasesDetail,
            PermissionKeys.PurchasesCreate,
            PermissionKeys.PurchasesEdit,
            PermissionKeys.PurchasesDelete,
            PermissionKeys.PurchasesImport,
            PermissionKeys.PurchasesExport,
            PermissionKeys.PurchasesPrint
        }),
        new GroupDefinition("CashBook", "Sổ quỹ", new[]
        {
            PermissionKeys.CashBookView,
            PermissionKeys.CashBookCreateReceipt,
            PermissionKeys.CashBookCreatePayment,
            PermissionKeys.CashBookEdit,
            PermissionKeys.CashBookDelete,
            PermissionKeys.CashBookCancel,
            PermissionKeys.CashBookBalance,
            PermissionKeys.CashBookInit,
            PermissionKeys.CashBookOpeningBalance,
            PermissionKeys.CashBookDetail,
            PermissionKeys.CashBookImport,
            PermissionKeys.CashBookExport
        }),
        new GroupDefinition("Reports", "Báo cáo", new[]
        {
            PermissionKeys.ReportsView,
            PermissionKeys.ReportsEndOfDay,
            PermissionKeys.ReportsSales,
            PermissionKeys.ReportsProducts,
            PermissionKeys.ReportsPurchases,
            PermissionKeys.ReportsRevenue,
            PermissionKeys.ReportsProfit,
            PermissionKeys.ReportsCost,
            PermissionKeys.ReportsCustomerDebt,
            PermissionKeys.ReportsSupplierDebt,
            PermissionKeys.ReportsCashFlow,
            PermissionKeys.ReportsPrint,
            PermissionKeys.ReportsExport
        }),
        new GroupDefinition("Users", "Quản trị tài khoản", new[]
        {
            PermissionKeys.UsersView,
            PermissionKeys.UsersCreate,
            PermissionKeys.UsersEdit,
            PermissionKeys.UsersLock,
            PermissionKeys.UsersUnlock,
            PermissionKeys.UsersDelete,
            PermissionKeys.UsersAssignPermission,
            PermissionKeys.UsersResetPassword,
            PermissionKeys.UsersChangePassword
        }),
        new GroupDefinition("Settings", "Cấu hình hệ thống", new[]
        {
            PermissionKeys.SettingsView,
            PermissionKeys.SettingsEdit,
            PermissionKeys.StoreInfoView,
            PermissionKeys.StoreInfoEdit
        })
    };

    /// <summary>Tất cả key chi tiết, không trùng lặp.</summary>
    public static readonly IReadOnlySet<string> AllKeys = Groups
        .SelectMany(g => g.Keys)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> _tenNhom =
        Groups.ToDictionary(g => g.Nhom, g => g.TenNhom, StringComparer.Ordinal);

    /// <summary>Nhãn tiếng Việt của từng quyền, khớp đúng danh mục ở trên.</summary>
    private static readonly Dictionary<string, string> _tenQuyen = new(StringComparer.Ordinal)
    {
        // Tổng quan
        [PermissionKeys.DashboardView] = "Xem màn hình tổng quan",

        // Bán hàng
        [PermissionKeys.SalesView] = "Xem màn hình bán hàng",
        [PermissionKeys.SalesSearchProduct] = "Tìm kiếm sản phẩm",
        [PermissionKeys.SalesAddProduct] = "Thêm sản phẩm vào đơn",
        [PermissionKeys.SalesEditQuantity] = "Sửa số lượng",
        [PermissionKeys.SalesRemoveProduct] = "Xóa sản phẩm khỏi đơn",
        [PermissionKeys.SalesDiscount] = "Giảm giá",
        [PermissionKeys.SalesPayment] = "Thanh toán",
        [PermissionKeys.SalesPrint] = "In hóa đơn",
        [PermissionKeys.SalesCancel] = "Hủy hóa đơn",
        [PermissionKeys.SalesHistory] = "Xem lịch sử bán hàng",

        // Hàng hóa
        [PermissionKeys.ProductsView] = "Xem danh sách hàng hóa",
        [PermissionKeys.ProductsDetail] = "Xem chi tiết hàng hóa",
        [PermissionKeys.ProductsCreate] = "Thêm hàng hóa",
        [PermissionKeys.ProductsEdit] = "Sửa hàng hóa",
        [PermissionKeys.ProductsDelete] = "Xóa hàng hóa",
        [PermissionKeys.ProductsImport] = "Import Excel",
        [PermissionKeys.ProductsExport] = "Export Excel",
        [PermissionKeys.ProductsAdjustStock] = "Điều chỉnh tồn kho",
        [PermissionKeys.ProductsUpdatePrice] = "Sửa giá bán",
        [PermissionKeys.ProductsUpdateCost] = "Sửa giá vốn",

        // Hóa đơn
        [PermissionKeys.InvoicesView] = "Xem hóa đơn",
        [PermissionKeys.InvoicesDetail] = "Xem chi tiết hóa đơn",
        [PermissionKeys.InvoicesCreate] = "Tạo hóa đơn",
        [PermissionKeys.InvoicesEdit] = "Sửa hóa đơn",
        [PermissionKeys.InvoicesCancel] = "Hủy hóa đơn",
        [PermissionKeys.InvoicesDelete] = "Xóa hóa đơn",
        [PermissionKeys.InvoicesPrint] = "In hóa đơn",
        [PermissionKeys.InvoicesImport] = "Import hóa đơn",
        [PermissionKeys.InvoicesExport] = "Export hóa đơn",

        // Khách hàng
        [PermissionKeys.CustomersView] = "Xem khách hàng",
        [PermissionKeys.CustomersDetail] = "Xem chi tiết khách hàng",
        [PermissionKeys.CustomersCreate] = "Thêm khách hàng",
        [PermissionKeys.CustomersEdit] = "Sửa khách hàng",
        [PermissionKeys.CustomersDelete] = "Xóa khách hàng",
        [PermissionKeys.CustomersImport] = "Import khách hàng",
        [PermissionKeys.CustomersExport] = "Export khách hàng",
        [PermissionKeys.CustomersDebtView] = "Xem công nợ",
        [PermissionKeys.CustomersDebtPay] = "Thu tiền khách hàng",

        // Nhà cung cấp
        [PermissionKeys.SuppliersView] = "Xem nhà cung cấp",
        [PermissionKeys.SuppliersDetail] = "Xem chi tiết nhà cung cấp",
        [PermissionKeys.SuppliersCreate] = "Thêm nhà cung cấp",
        [PermissionKeys.SuppliersEdit] = "Sửa nhà cung cấp",
        [PermissionKeys.SuppliersDelete] = "Xóa nhà cung cấp",
        [PermissionKeys.SuppliersImport] = "Import nhà cung cấp",
        [PermissionKeys.SuppliersExport] = "Export nhà cung cấp",
        [PermissionKeys.SuppliersDebtView] = "Xem công nợ",
        [PermissionKeys.SuppliersDebtPay] = "Thanh toán công nợ",

        // Phiếu nhập hàng
        [PermissionKeys.PurchasesView] = "Xem phiếu nhập hàng",
        [PermissionKeys.PurchasesDetail] = "Xem chi tiết phiếu nhập",
        [PermissionKeys.PurchasesCreate] = "Tạo phiếu nhập",
        [PermissionKeys.PurchasesEdit] = "Sửa phiếu nhập",
        [PermissionKeys.PurchasesDelete] = "Xóa phiếu nhập",
        [PermissionKeys.PurchasesImport] = "Import phiếu nhập",
        [PermissionKeys.PurchasesExport] = "Export phiếu nhập",
        [PermissionKeys.PurchasesPrint] = "In phiếu nhập",

        // Sổ quỹ
        [PermissionKeys.CashBookView] = "Xem sổ quỹ",
        [PermissionKeys.CashBookCreateReceipt] = "Thêm phiếu thu",
        [PermissionKeys.CashBookCreatePayment] = "Thêm phiếu chi",
        [PermissionKeys.CashBookEdit] = "Sửa phiếu",
        [PermissionKeys.CashBookDelete] = "Xóa phiếu",
        [PermissionKeys.CashBookCancel] = "Hủy phiếu",
        [PermissionKeys.CashBookBalance] = "Xem số dư",
        [PermissionKeys.CashBookInit] = "Khởi tạo sổ quỹ",
        [PermissionKeys.CashBookOpeningBalance] = "Nhập số dư đầu kỳ",
        [PermissionKeys.CashBookDetail] = "Xem chi tiết phiếu",
        [PermissionKeys.CashBookImport] = "Import sổ quỹ",
        [PermissionKeys.CashBookExport] = "Export sổ quỹ",

        // Báo cáo
        [PermissionKeys.ReportsView] = "Xem báo cáo",
        [PermissionKeys.ReportsEndOfDay] = "Báo cáo cuối ngày",
        [PermissionKeys.ReportsSales] = "Báo cáo bán hàng",
        [PermissionKeys.ReportsProducts] = "Báo cáo hàng hóa",
        [PermissionKeys.ReportsPurchases] = "Báo cáo phiếu nhập hàng",
        [PermissionKeys.ReportsRevenue] = "Xem doanh thu",
        [PermissionKeys.ReportsProfit] = "Xem lợi nhuận",
        [PermissionKeys.ReportsCost] = "Xem giá vốn",
        [PermissionKeys.ReportsCustomerDebt] = "Xem công nợ khách hàng",
        [PermissionKeys.ReportsSupplierDebt] = "Xem công nợ nhà cung cấp",
        [PermissionKeys.ReportsCashFlow] = "Xem dòng tiền",
        [PermissionKeys.ReportsPrint] = "In báo cáo",
        [PermissionKeys.ReportsExport] = "Export báo cáo",

        // Tài khoản
        [PermissionKeys.UsersView] = "Xem tài khoản",
        [PermissionKeys.UsersCreate] = "Thêm tài khoản",
        [PermissionKeys.UsersEdit] = "Sửa tài khoản",
        [PermissionKeys.UsersLock] = "Khóa tài khoản",
        [PermissionKeys.UsersUnlock] = "Mở khóa tài khoản",
        [PermissionKeys.UsersDelete] = "Xóa tài khoản",
        [PermissionKeys.UsersAssignPermission] = "Phân quyền tài khoản",
        [PermissionKeys.UsersResetPassword] = "Đặt lại mật khẩu",
        [PermissionKeys.UsersChangePassword] = "Tự đổi mật khẩu",

        // Cấu hình hệ thống
        [PermissionKeys.SettingsView] = "Xem cấu hình hệ thống",
        [PermissionKeys.SettingsEdit] = "Cấu hình hệ thống",
        [PermissionKeys.StoreInfoView] = "Xem thông tin tiệm",
        [PermissionKeys.StoreInfoEdit] = "Sửa thông tin tiệm"
    };

    /// <summary>Key quyền "xem" dùng để quyết định có hiện module trong menu hay không.</summary>
    public static readonly IReadOnlyDictionary<string, string> ModuleViewKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Dashboard"] = PermissionKeys.DashboardView,
            ["Sales"] = PermissionKeys.SalesView,
            ["Products"] = PermissionKeys.ProductsView,
            ["Invoices"] = PermissionKeys.InvoicesView,
            ["Customers"] = PermissionKeys.CustomersView,
            ["Suppliers"] = PermissionKeys.SuppliersView,
            ["Purchases"] = PermissionKeys.PurchasesView,
            ["CashBook"] = PermissionKeys.CashBookView,
            ["Reports"] = PermissionKeys.ReportsView,
            ["Users"] = PermissionKeys.UsersView,
            ["Settings"] = PermissionKeys.SettingsView
        };

    /// <summary>
    /// Nhóm quyền mặc định cho từng mã vai trò. Khi tạo tài khoản, chọn vai trò nào
    /// thì cây quyền tự tick sẵn các quyền dưới đây (người dùng vẫn sửa lại được).
    /// Đây chỉ là gợi ý/điểm khởi đầu, KHÔNG phải kiểm tra quyền cứng theo vai trò.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["QUAN_TRI"] = AllKeys.ToArray(),
            ["QUAN_LY"] = new[]
            {
                PermissionKeys.DashboardView,
                PermissionKeys.SalesView, PermissionKeys.SalesSearchProduct,
                PermissionKeys.SalesAddProduct, PermissionKeys.SalesEditQuantity,
                PermissionKeys.SalesRemoveProduct, PermissionKeys.SalesDiscount,
                PermissionKeys.SalesPayment, PermissionKeys.SalesPrint, PermissionKeys.SalesHistory,
                PermissionKeys.ProductsView, PermissionKeys.ProductsDetail,
                PermissionKeys.ProductsCreate, PermissionKeys.ProductsEdit, PermissionKeys.ProductsDelete,
                PermissionKeys.ProductsImport, PermissionKeys.ProductsExport,
                PermissionKeys.ProductsAdjustStock, PermissionKeys.ProductsUpdatePrice,
                PermissionKeys.InvoicesView, PermissionKeys.InvoicesDetail,
                PermissionKeys.InvoicesCreate, PermissionKeys.InvoicesEdit,
                PermissionKeys.InvoicesCancel, PermissionKeys.InvoicesPrint, PermissionKeys.InvoicesExport,
                PermissionKeys.CustomersView, PermissionKeys.CustomersDetail,
                PermissionKeys.CustomersCreate, PermissionKeys.CustomersEdit, PermissionKeys.CustomersDelete,
                PermissionKeys.CustomersImport, PermissionKeys.CustomersExport,
                PermissionKeys.CustomersDebtView, PermissionKeys.CustomersDebtPay,
                PermissionKeys.SuppliersView, PermissionKeys.SuppliersDetail,
                PermissionKeys.SuppliersCreate, PermissionKeys.SuppliersEdit, PermissionKeys.SuppliersDelete,
                PermissionKeys.SuppliersImport, PermissionKeys.SuppliersExport,
                PermissionKeys.SuppliersDebtView, PermissionKeys.SuppliersDebtPay,
                PermissionKeys.PurchasesView, PermissionKeys.PurchasesDetail,
                PermissionKeys.PurchasesCreate, PermissionKeys.PurchasesEdit, PermissionKeys.PurchasesDelete,
                PermissionKeys.PurchasesImport, PermissionKeys.PurchasesExport, PermissionKeys.PurchasesPrint,
                PermissionKeys.CashBookView, PermissionKeys.CashBookCreateReceipt,
                PermissionKeys.CashBookCreatePayment, PermissionKeys.CashBookEdit,
                PermissionKeys.CashBookDelete, PermissionKeys.CashBookCancel, PermissionKeys.CashBookBalance,
                PermissionKeys.CashBookImport, PermissionKeys.CashBookExport,
                PermissionKeys.ReportsView, PermissionKeys.ReportsEndOfDay, PermissionKeys.ReportsSales,
                PermissionKeys.ReportsProducts, PermissionKeys.ReportsPurchases, PermissionKeys.ReportsRevenue,
                PermissionKeys.ReportsProfit, PermissionKeys.ReportsCost, PermissionKeys.ReportsPrint,
                PermissionKeys.ReportsExport,
                PermissionKeys.UsersChangePassword,
                PermissionKeys.SettingsView, PermissionKeys.SettingsEdit,
                PermissionKeys.StoreInfoView, PermissionKeys.StoreInfoEdit
            },
            ["THU_NGAN"] = new[]
            {
                PermissionKeys.DashboardView,
                PermissionKeys.SalesView, PermissionKeys.SalesSearchProduct,
                PermissionKeys.SalesAddProduct, PermissionKeys.SalesEditQuantity,
                PermissionKeys.SalesRemoveProduct, PermissionKeys.SalesPayment, PermissionKeys.SalesPrint,
                PermissionKeys.ProductsView, PermissionKeys.ProductsDetail,
                PermissionKeys.InvoicesView, PermissionKeys.InvoicesDetail,
                PermissionKeys.InvoicesCreate, PermissionKeys.InvoicesPrint,
                PermissionKeys.CustomersView, PermissionKeys.CustomersDetail,
                PermissionKeys.CustomersCreate, PermissionKeys.CustomersEdit,
                PermissionKeys.CustomersDebtView, PermissionKeys.CustomersDebtPay,
                PermissionKeys.CashBookView, PermissionKeys.CashBookCreateReceipt,
                PermissionKeys.CashBookCreatePayment, PermissionKeys.CashBookBalance,
                PermissionKeys.CashBookDetail,
                PermissionKeys.ReportsView, PermissionKeys.ReportsEndOfDay,
                PermissionKeys.ReportsSales, PermissionKeys.ReportsPrint,
                PermissionKeys.UsersChangePassword
            },
            ["KHO"] = new[]
            {
                PermissionKeys.DashboardView,
                PermissionKeys.ProductsView, PermissionKeys.ProductsDetail,
                PermissionKeys.ProductsCreate, PermissionKeys.ProductsEdit, PermissionKeys.ProductsDelete,
                PermissionKeys.ProductsImport, PermissionKeys.ProductsExport,
                PermissionKeys.ProductsAdjustStock, PermissionKeys.ProductsUpdatePrice,
                PermissionKeys.ProductsUpdateCost,
                PermissionKeys.PurchasesView, PermissionKeys.PurchasesDetail,
                PermissionKeys.PurchasesCreate, PermissionKeys.PurchasesEdit, PermissionKeys.PurchasesDelete,
                PermissionKeys.PurchasesImport, PermissionKeys.PurchasesExport, PermissionKeys.PurchasesPrint,
                PermissionKeys.SuppliersView, PermissionKeys.SuppliersDetail,
                PermissionKeys.SuppliersCreate, PermissionKeys.SuppliersEdit,
                PermissionKeys.SuppliersImport, PermissionKeys.SuppliersExport,
                PermissionKeys.SuppliersDebtView,
                PermissionKeys.ReportsView, PermissionKeys.ReportsProducts,
                PermissionKeys.ReportsPurchases, PermissionKeys.ReportsCost, PermissionKeys.ReportsPrint,
                PermissionKeys.UsersChangePassword
            },
            ["BAO_CAO"] = new[]
            {
                PermissionKeys.DashboardView,
                PermissionKeys.ReportsView, PermissionKeys.ReportsEndOfDay, PermissionKeys.ReportsSales,
                PermissionKeys.ReportsProducts, PermissionKeys.ReportsPurchases, PermissionKeys.ReportsRevenue,
                PermissionKeys.ReportsProfit, PermissionKeys.ReportsCost, PermissionKeys.ReportsCustomerDebt,
                PermissionKeys.ReportsSupplierDebt, PermissionKeys.ReportsCashFlow,
                PermissionKeys.ReportsPrint, PermissionKeys.ReportsExport,
                PermissionKeys.UsersChangePassword
            }
        };

    public static string GetTenNhom(string? nhom)
    {
        if (string.IsNullOrWhiteSpace(nhom))
        {
            return "Khác";
        }

        return _tenNhom.TryGetValue(nhom, out var ten) ? ten : nhom;
    }

    public static string GetTenQuyen(string? maQuyen)
    {
        if (string.IsNullOrWhiteSpace(maQuyen))
        {
            return string.Empty;
        }

        return _tenQuyen.TryGetValue(maQuyen, out var ten) ? ten : maQuyen;
    }

    public static int GetThuTuNhom(string? nhom)
    {
        for (var i = 0; i < Groups.Count; i++)
        {
            if (string.Equals(Groups[i].Nhom, nhom, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>Thứ tự hiển thị của một quyền trong nhóm (dùng khi seed database).</summary>
    public static int GetThuTuQuyen(string maQuyen)
    {
        foreach (var g in Groups)
        {
            for (var i = 0; i < g.Keys.Length; i++)
            {
                if (string.Equals(g.Keys[i], maQuyen, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }
        }

        return 0;
    }

    /// <summary>Loại hành động suy ra từ hậu tố key (dùng khi seed database).</summary>
    public static string GetLoai(string maQuyen)
    {
        var dot = maQuyen.IndexOf('.');
        return dot >= 0 && dot < maQuyen.Length - 1 ? maQuyen[(dot + 1)..] : maQuyen;
    }
}
