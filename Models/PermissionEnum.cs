namespace ShopHoa.Models;

/// <summary>
/// Danh sách quyền chi tiết của phần mềm, đặt theo quy ước
/// <c>&lt;menu&gt;.&lt;chuc_nang&gt;</c> (ví dụ: <c>banhang.thanh_toan</c>).
/// <para>
/// Mỗi hằng ở đây tương ứng MỘT dòng trong bảng <c>quyen</c>.
/// Thêm chức năng mới thì thêm key ở đây + khai báo vào
/// <see cref="Services.PermissionCatalog"/> (nhóm hiển thị) thì hệ thống
/// (menu, cây phân quyền, kiểm tra quyền) tự nhận.
/// </para>
/// Không hard-code quyền theo tên vai trò: vai trò chỉ là nhóm quyền mặc định.
/// </summary>
public static class PermissionKeys
{
    // ===== Tổng quan =====
    public const string DashboardView = "tongquan.xem";

    // ===== Bán hàng =====
    public const string SalesView = "banhang.xem";
    public const string SalesSearchProduct = "banhang.tim_san_pham";
    public const string SalesAddProduct = "banhang.them_san_pham";
    public const string SalesEditQuantity = "banhang.sua_so_luong";
    public const string SalesRemoveProduct = "banhang.xoa_san_pham";
    public const string SalesDiscount = "banhang.giam_gia";
    public const string SalesPayment = "banhang.thanh_toan";
    public const string SalesPrint = "banhang.in_hoa_don";
    public const string SalesCancel = "banhang.huy_hoa_don";
    public const string SalesHistory = "banhang.xem_lich_su";

    // ===== Hàng hóa =====
    public const string ProductsView = "hanghoa.xem";
    public const string ProductsDetail = "hanghoa.xem_chi_tiet";
    public const string ProductsCreate = "hanghoa.them";
    public const string ProductsEdit = "hanghoa.sua";
    public const string ProductsDelete = "hanghoa.xoa";
    public const string ProductsImport = "hanghoa.import";
    public const string ProductsExport = "hanghoa.export";
    public const string ProductsAdjustStock = "hanghoa.dieu_chinh_ton_kho";
    public const string ProductsUpdatePrice = "hanghoa.sua_gia_ban";
    public const string ProductsUpdateCost = "hanghoa.sua_gia_von";

    // ===== Hóa đơn =====
    public const string InvoicesView = "hoadon.xem";
    public const string InvoicesDetail = "hoadon.xem_chi_tiet";
    public const string InvoicesCreate = "hoadon.tao";
    public const string InvoicesEdit = "hoadon.sua";
    public const string InvoicesCancel = "hoadon.huy";
    public const string InvoicesDelete = "hoadon.xoa";
    public const string InvoicesPrint = "hoadon.in";
    public const string InvoicesImport = "hoadon.import";
    public const string InvoicesExport = "hoadon.export";

    // ===== Khách hàng =====
    public const string CustomersView = "khachhang.xem";
    public const string CustomersDetail = "khachhang.xem_chi_tiet";
    public const string CustomersCreate = "khachhang.them";
    public const string CustomersEdit = "khachhang.sua";
    public const string CustomersDelete = "khachhang.xoa";
    public const string CustomersImport = "khachhang.import";
    public const string CustomersExport = "khachhang.export";
    public const string CustomersDebtView = "khachhang.xem_cong_no";
    public const string CustomersDebtPay = "khachhang.thu_tien";

    // ===== Nhà cung cấp =====
    public const string SuppliersView = "nhacungcap.xem";
    public const string SuppliersDetail = "nhacungcap.xem_chi_tiet";
    public const string SuppliersCreate = "nhacungcap.them";
    public const string SuppliersEdit = "nhacungcap.sua";
    public const string SuppliersDelete = "nhacungcap.xoa";
    public const string SuppliersImport = "nhacungcap.import";
    public const string SuppliersExport = "nhacungcap.export";
    public const string SuppliersDebtView = "nhacungcap.xem_cong_no";
    public const string SuppliersDebtPay = "nhacungcap.thanh_toan";

    // ===== Phiếu nhập hàng =====
    public const string PurchasesView = "nhaphang.xem";
    public const string PurchasesDetail = "nhaphang.xem_chi_tiet";
    public const string PurchasesCreate = "nhaphang.tao";
    public const string PurchasesEdit = "nhaphang.sua";
    public const string PurchasesDelete = "nhaphang.xoa";
    public const string PurchasesImport = "nhaphang.import";
    public const string PurchasesExport = "nhaphang.export";
    public const string PurchasesPrint = "nhaphang.in";

    // ===== Sổ quỹ =====
    public const string CashBookView = "soquy.xem";
    public const string CashBookCreateReceipt = "soquy.them_phieu_thu";
    public const string CashBookCreatePayment = "soquy.them_phieu_chi";
    public const string CashBookEdit = "soquy.sua_phieu";
    public const string CashBookDelete = "soquy.xoa_phieu";
    public const string CashBookCancel = "soquy.huy_phieu";
    public const string CashBookBalance = "soquy.xem_so_du";
    public const string CashBookInit = "soquy.khoi_tao";
    public const string CashBookOpeningBalance = "soquy.so_du_dau_ky";
    public const string CashBookDetail = "soquy.xem_chi_tiet";
    public const string CashBookImport = "soquy.import";
    public const string CashBookExport = "soquy.export";

    // ===== Báo cáo =====
    public const string ReportsView = "baocao.xem";
    public const string ReportsEndOfDay = "baocao.bao_cao_cuoi_ngay";
    public const string ReportsSales = "baocao.bao_cao_ban_hang";
    public const string ReportsProducts = "baocao.bao_cao_hang_hoa";
    public const string ReportsPurchases = "baocao.bao_cao_phieu_nhap";
    public const string ReportsRevenue = "baocao.xem_doanh_thu";
    public const string ReportsProfit = "baocao.xem_loi_nhuan";
    public const string ReportsCost = "baocao.xem_gia_von";
    public const string ReportsCustomerDebt = "baocao.cong_no_khach_hang";
    public const string ReportsSupplierDebt = "baocao.cong_no_nha_cung_cap";
    public const string ReportsCashFlow = "baocao.dong_tien";
    public const string ReportsPrint = "baocao.in";
    public const string ReportsExport = "baocao.export";

    // ===== Quản trị hệ thống / Tài khoản =====
    public const string UsersView = "taikhoan.xem";
    public const string UsersCreate = "taikhoan.them";
    public const string UsersEdit = "taikhoan.sua";
    public const string UsersLock = "taikhoan.khoa";
    public const string UsersUnlock = "taikhoan.mo_khoa";
    public const string UsersDelete = "taikhoan.xoa";
    public const string UsersAssignPermission = "taikhoan.phan_quyen";
    public const string UsersResetPassword = "taikhoan.dat_lai_mat_khau";
    public const string UsersChangePassword = "taikhoan.doi_mat_khau";

    // ===== Cấu hình hệ thống =====
    public const string SettingsView = "caidat.xem";
    public const string SettingsEdit = "caidat.sua";

    // ===== Thông tin tiệm =====
    public const string StoreInfoView = "thongtin.xem";
    public const string StoreInfoEdit = "thongtin.sua";
}
