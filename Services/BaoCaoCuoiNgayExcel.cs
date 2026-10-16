using ClosedXML.Excel;
using ShopHoa.Models;

namespace ShopHoa.Services;

/// <summary>
/// Dựng file Excel cho báo cáo cuối ngày: đúng 6 sheet theo thứ tự
/// Tổng quan, Hóa đơn bán hàng, Sản phẩm bán chạy, Thu chi,
/// Công nợ khách hàng, Nhập hàng.
/// <para>
/// Tách riêng khỏi ViewModel để kiểm thử được định dạng file mà không cần
/// mở hộp thoại lưu và không cần chạy giao diện.
/// </para>
/// </summary>
public static class BaoCaoCuoiNgayExcel
{
    private const string MauTieuDe = "#164B40";
    private const string DinhDangTien = "#,##0";
    private const string DinhDangNgay = "dd/MM/yyyy HH:mm";

    /// <summary>Bộ dòng tiêu đề cố định của từng sheet, dùng cả cho tiêu đề và kiểm thử.</summary>
    public static readonly string[][] TenSheetVaTieuDe =
    [
        ["Tổng quan"],
        ["Hóa đơn bán hàng"],
        ["Sản phẩm bán chạy"],
        ["Thu chi"],
        ["Công nợ khách hàng"],
        ["Nhập hàng"]
    ];

    /// <summary>Ghi đủ 6 sheet vào workbook (workbook phải còn trống).</summary>
    public static void GhiTatCa(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        GhiSheetTongQuan(workbook, duLieu);
        GhiSheetHoaDon(workbook, duLieu);
        GhiSheetSanPham(workbook, duLieu);
        GhiSheetThuChi(workbook, duLieu);
        GhiSheetCongNo(workbook, duLieu);
        GhiSheetNhapHang(workbook, duLieu);
    }

    /// <summary>Dựng workbook hoàn chỉnh từ dữ liệu báo cáo.</summary>
    public static XLWorkbook TaoWorkbook(BaoCaoCuoiNgayDuLieu duLieu)
    {
        var workbook = new XLWorkbook();
        GhiTatCa(workbook, duLieu);
        return workbook;
    }


    // ============================================================
    // TIỆN ÍCH CHUNG
    // ============================================================

    /// <summary>
    /// Ghi dòng tiêu đề đã định dạng (đậm, nền xanh, canh giữa) từ danh sách tên cột.
    /// </summary>
    private static void GhiTieuDe(IXLWorksheet sheet, string[] tieuDe)
    {
        for (var c = 0; c < tieuDe.Length; c++)
        {
            var o = sheet.Cell(1, c + 1);
            o.Value = tieuDe[c];
            o.Style.Font.Bold = true;
            o.Style.Font.FontColor = XLColor.White;
            o.Style.Fill.BackgroundColor = XLColor.FromHtml(MauTieuDe);
            o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            o.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        sheet.Row(1).Height = 22;
        sheet.SheetView.FreezeRows(1);
    }

    /// <summary>Định dạng một ô là tiền: số thập phân thuần, căn phải.</summary>
    private static void GhiTien(IXLCell o, decimal giaTri)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = DinhDangTien;
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    /// <summary>Định dạng một ô là số lượng (cho phép phần thập phân của kg/lít).</summary>
    private static void GhiSoLuong(IXLCell o, decimal giaTri)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = "#,##0.##";
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    /// <summary>Định dạng một ô là ngày giờ.</summary>
    private static void GhiNgay(IXLCell o, DateTime giaTri)
    {
        o.Value = giaTri;
        o.Style.NumberFormat.Format = DinhDangNgay;
        o.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Tự co độ rộng cột trong khoảng [doRongToiThieu, doRongToiDa].</summary>
    private static void ChinhDoRongCot(IXLWorksheet sheet, int soCot, int dongCuoi, double toiThieu, double toiDa)
    {
        for (var c = 1; c <= soCot; c++)
            sheet.Column(c).AdjustToContents(1, dongCuoi, toiThieu, toiDa);
    }

    /// <summary>
    /// Ghi một cặp "nhãn – giá trị" vào hai cột, dùng cho các sheet tổng hợp.
    /// Tiền ghi bằng số thập phân thuần để người dùng cộng/lọc được.
    /// </summary>
    private static void GhiDongGiaTri(IXLWorksheet sheet, int dong, string nhan, object giaTri, bool laTien = false)
    {
        var oNhan = sheet.Cell(dong, 1);
        oNhan.Value = nhan;
        oNhan.Style.Font.Bold = true;

        var oGiaTri = sheet.Cell(dong, 2);

        switch (giaTri)
        {
            case decimal tien when laTien:
                GhiTien(oGiaTri, tien);
                return;

            case decimal tienSo:
                oGiaTri.Value = tienSo;
                return;

            case int so:
                oGiaTri.Value = so;
                return;

            case string chuoi:
                oGiaTri.Value = chuoi;
                return;

            default:
                oGiaTri.Value = giaTri?.ToString() ?? "";
                return;
        }
    }

    // ============================================================
    // SHEET 1 — TỔNG QUAN
    // ============================================================

    private static void GhiSheetTongQuan(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        var sheet = workbook.Worksheets.Add("Tổng quan");

        var banHang = duLieu.TongKetBanHang;
        var soQuy = duLieu.SoQuyTongHop;
        var nhap = duLieu.NhapHang;
        var khach = duLieu.KhachHangTongHop;
        var congNo = duLieu.CongNoTongHop;

        GhiTieuDe(sheet, ["Chỉ tiêu", "Giá trị", "Ghi chú"]);

        var dong = 2;

        // --- Tiêu đề nhóm: doanh thu ---
        GhiDongTieuDeNhom(sheet, dong++, "1. BÁN HÀNG (hóa đơn trạng thái HOAN_THANH)");

        // Giữ số thứ tự dòng để gắn ghi chú đúng chỗ, không suy ra bằng
        // trừ số dòng (dễ lệch khi thêm/bớt chỉ tiêu).
        var dTongTienHang = dong++;
        GhiDongGiaTri(sheet, dTongTienHang, "Tổng tiền hàng", banHang.TongTienHang, true);

        var dGiamGia = dong++;
        GhiDongGiaTri(sheet, dGiamGia, "Giảm giá", banHang.GiamGia, true);

        var dDoanhThu = dong++;
        GhiDongGiaTri(sheet, dDoanhThu, "Doanh thu (khach_can_tra)", banHang.DoanhThu, true);

        var dDaThu = dong++;
        GhiDongGiaTri(sheet, dDaThu, "Đã thu (khach_da_thanh_toan)", banHang.DaThu, true);

        var dConNo = dong++;
        GhiDongGiaTri(sheet, dConNo, "Còn nợ (con_no)", banHang.ConNo, true);

        var dTienThua = dong++;
        GhiDongGiaTri(sheet, dTienThua, "Tiền thối (thu_khac)", banHang.TienThua, true);

        GhiDongGiaTri(sheet, dong++, "Số hóa đơn", banHang.SoHoaDon);
        GhiDongGiaTri(sheet, dong++, "Hóa đơn đã thanh toán", banHang.SoHoaDonDaThanhToan);
        GhiDongGiaTri(sheet, dong++, "Hóa đơn còn công nợ", banHang.SoHoaDonConCongNo);

        sheet.Cell(dTienThua, 3).Value = "Tiền thối KHÔNG tính vào doanh thu";
        sheet.Cell(dDoanhThu, 3).Value = "Doanh thu = tổng tiền hàng − giảm giá";
        sheet.Cell(dDaThu, 3).Value = "Đã thu + còn nợ = doanh thu";

        // --- Tiêu đề nhóm: sổ quỹ ---
        GhiDongTieuDeNhom(sheet, dong++, "2. SỔ QUỸ (phieu_chi, phiếu HOAN_THANH)");

        GhiDongGiaTri(sheet, dong++, "Tổng thu", soQuy.TongThu, true);
        GhiDongGiaTri(sheet, dong++, "Tổng chi", soQuy.TongChi, true);
        var dChenhLech = dong++;
        GhiDongGiaTri(sheet, dChenhLech, "Chênh lệch trong ngày (thu − chi)", soQuy.ChenhLech, true);

        GhiDongGiaTri(sheet, dong++, "Số phiếu thu (PT)", soQuy.SoPhieuThu);
        GhiDongGiaTri(sheet, dong++, "Số phiếu chi (PC)", soQuy.SoPhieuChi);

        sheet.Cell(dChenhLech, 3).Value = "Sổ quỹ là dòng tiền thực, KHÁC với \"đã thu\" trên hóa đơn";

        // --- Tiêu đề nhóm: công nợ ---
        GhiDongTieuDeNhom(sheet, dong++, "3. CÔNG NỢ KHÁCH HÀNG phát sinh trong ngày");

        GhiDongGiaTri(sheet, dong++, "Tổng giá trị hóa đơn", congNo.TongCongNoPhatSinh, true);
        GhiDongGiaTri(sheet, dong++, "Đã thu trong ngày", congNo.TongDaTra, true);
        GhiDongGiaTri(sheet, dong++, "Còn nợ cuối ngày", congNo.TongConNo, true);
        GhiDongGiaTri(sheet, dong++, "Số hóa đơn", congNo.SoHoaDon);
        GhiDongGiaTri(sheet, dong++, "Số hóa đơn còn nợ", congNo.SoHoaDonConNo);

        // --- Tiêu đề nhóm: khách hàng ---
        GhiDongTieuDeNhom(sheet, dong++, "4. KHÁCH HÀNG");

        GhiDongGiaTri(sheet, dong++, "Số khách có hồ sơ đã mua", khach.SoKhachDaMua);
        GhiDongGiaTri(sheet, dong++, "Khách mới (lần mua đầu trong ngày)", khach.SoKhachMoi);
        GhiDongGiaTri(sheet, dong++, "Khách quay lại", khach.SoKhachQuayLai);
        var dTongGiaTriKhach = dong++;
        GhiDongGiaTri(sheet, dTongGiaTriKhach, "Tổng giá trị mua của khách có hồ sơ", khach.TongGiaTriMua, true);

        sheet.Cell(dTongGiaTriKhach, 3).Value = "Khách lẻ (không có hồ sơ) không tính vào mới/quay lại";

        // --- Tiêu đề nhóm: nhập hàng ---
        GhiDongTieuDeNhom(sheet, dong++, "5. NHẬP HÀNG (loại trừ phiếu HUY)");

        GhiDongGiaTri(sheet, dong++, "Số phiếu nhập", nhap.SoPhieu);
        GhiDongGiaTri(sheet, dong++, "Tổng tiền nhập", nhap.TongTienNhap, true);
        GhiDongGiaTri(sheet, dong++, "Đã trả nhà cung cấp", nhap.DaThanhToan, true);
        GhiDongGiaTri(sheet, dong++, "Còn nợ nhà cung cấp", nhap.ConNo, true);

        // --- Tiêu đề nhóm: phương thức thanh toán ---
        GhiDongTieuDeNhom(sheet, dong++, "6. PHƯƠNG THỨC THANH TOÁN (theo sổ quỹ)");

        foreach (var p in duLieu.PhuongThucThanhToan)
        {
            GhiDongGiaTri(sheet, dong++, $"{p.TenPhuongThucHienThi} ({p.SoGiaoDich} phiếu)", p.SoTien, true);
        }

        if (duLieu.PhuongThucThanhToan.Count == 0)
            GhiDongGiaTri(sheet, dong++, "Không có phiếu thu trong ngày", "");

        sheet.Cell(dong - (duLieu.PhuongThucThanhToan.Count > 0 ? duLieu.PhuongThucThanhToan.Count : 1), 3).Value =
            "Lấy từ phieu_chi.phuong_thuc — là dòng tiền sổ quỹ, không phải doanh thu hóa đơn";

        // Định dạng cột tiền + ghi chú.
        for (var d = 2; d < dong; d++)
        {
            sheet.Cell(d, 3).Style.Alignment.WrapText = false;
        }

        sheet.Column(1).Width = 44;
        sheet.Column(2).Width = 22;
        sheet.Column(3).Width = 58;
        sheet.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    /// <summary>Ghi một dòng tiêu đề nhóm, đậm và nền xanh nhạt.</summary>
    private static void GhiDongTieuDeNhom(IXLWorksheet sheet, int dong, string tieuDe)
    {
        var o = sheet.Cell(dong, 1);
        o.Value = tieuDe;
        o.Style.Font.Bold = true;
        o.Style.Font.FontColor = XLColor.FromHtml("#164B40");
        o.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF9F5");

        sheet.Range(dong, 1, dong, 3).Merge();
    }

    // ============================================================
    // SHEET 2 — HÓA ĐƠN BÁN HÁNG
    // ============================================================

    private static void GhiSheetHoaDon(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        var sheet = workbook.Worksheets.Add("Hóa đơn bán hàng");

        // Chỉ ghi những cột lấy được thật từ database. Không có cột phương thức
        // thanh toán theo hóa đơn (bảng thanh_toan rỗng), nên không bịa cột đó —
        // phương thức chỉ có ở sheet "Thu chi" theo sổ quỹ.
        GhiTieuDe(sheet,
        [
            "Mã hóa đơn", "Thời gian", "Khách hàng", "Tổng tiền hàng", "Giảm giá",
            "Doanh thu", "Đã thanh toán", "Còn nợ", "Tiền thối", "Trạng thái"
        ]);

        // Danh sách hóa đơn hoàn thành trong ngày.
        var ds = duLieu.CongNoPhatSinh;

        for (var i = 0; i < ds.Count; i++)
        {
            var hd = ds[i];
            var dong = i + 2;

            sheet.Cell(dong, 1).Value = hd.MaHoaDon;
            GhiNgay(sheet.Cell(dong, 2), hd.ThoiGian);
            sheet.Cell(dong, 3).Value = hd.TenKhachHang;

            GhiTien(sheet.Cell(dong, 4), hd.TongTienHang);
            GhiTien(sheet.Cell(dong, 5), hd.GiamGia);
            GhiTien(sheet.Cell(dong, 6), hd.DoanhThu);
            GhiTien(sheet.Cell(dong, 7), hd.DaTra);
            GhiTien(sheet.Cell(dong, 8), hd.ConNo);
            GhiTien(sheet.Cell(dong, 9), hd.ThuKhac);
            sheet.Cell(dong, 10).Value = hd.TrangThai;

            sheet.Cell(dong, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Dòng tổng kết để người dùng đối chiếu nhanh với sheet Tổng quan.
        var dongTong = 2 + ds.Count;
        var tong = duLieu.TongKetBanHang;

        sheet.Cell(dongTong, 3).Value = "TỔNG CỘNG";
        sheet.Cell(dongTong, 3).Style.Font.Bold = true;
        GhiTien(sheet.Cell(dongTong, 4), tong.TongTienHang);
        GhiTien(sheet.Cell(dongTong, 5), tong.GiamGia);
        GhiTien(sheet.Cell(dongTong, 6), tong.DoanhThu);
        GhiTien(sheet.Cell(dongTong, 7), tong.DaThu);
        GhiTien(sheet.Cell(dongTong, 8), tong.ConNo);
        GhiTien(sheet.Cell(dongTong, 9), tong.TienThua);
        sheet.Range(dongTong, 3, dongTong, 10).Style.Font.Bold = true;
        sheet.Range(dongTong, 1, dongTong, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6F5");
        sheet.Range(dongTong, 1, dongTong, 10).Style.Border.TopBorder = XLBorderStyleValues.Double;

        ChinhDoRongCot(sheet, 10, dongTong, 14, 34);
        sheet.Column(3).Width = 28;

        // Ngày không có hóa đơn: dòng tổng vẫn giữ nguyên giá trị 0, thông báo
        // nằm ở dòng kế tiếp để không ghi đè mất dòng tổng.
        if (ds.Count == 0)
        {
            var dong = dongTong + 1;
            sheet.Cell(dong, 1).Value = "Không có hóa đơn hoàn thành trong ngày này.";
            sheet.Range(dong, 1, dong, 10).Merge();
            sheet.Cell(dong, 1).Style.Font.Italic = true;
        }
    }

    // ============================================================
    // SHEET 3 — SẢN PHẨM BÁN CHẠY
    // ============================================================

    private static void GhiSheetSanPham(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        var sheet = workbook.Worksheets.Add("Sản phẩm bán chạy");

        GhiTieuDe(sheet, ["Hạng", "Mã sản phẩm", "Tên sản phẩm", "Số lượng bán", "Doanh thu", "Tỉ lệ %"]);

        var ds = duLieu.TopSanPham;

        for (var i = 0; i < ds.Count; i++)
        {
            var sp = ds[i];
            var dong = i + 2;

            sheet.Cell(dong, 1).Value = sp.ThuTu;
            sheet.Cell(dong, 2).Value = sp.MaSanPham;
            sheet.Cell(dong, 3).Value = sp.Ten;

            GhiSoLuong(sheet.Cell(dong, 4), sp.SoLuong);
            GhiTien(sheet.Cell(dong, 5), sp.DoanhThu);

            sheet.Cell(dong, 6).Value = Math.Round(sp.TyLePhanTram, 2);
            sheet.Cell(dong, 6).Style.NumberFormat.Format = "0.00\"%\"";
            sheet.Cell(dong, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            sheet.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var dongCuoi = 1 + ds.Count;

        ChinhDoRongCot(sheet, 6, dongCuoi, 10, 38);

        if (ds.Count == 0)
        {
            sheet.Cell(2, 1).Value = "Không có sản phẩm nào bán trong ngày này.";
            sheet.Range(2, 1, 2, 6).Merge();
            sheet.Cell(2, 1).Style.Font.Italic = true;
        }
    }

    // ============================================================
    // SHEET 4 — THU CHI
    // ============================================================

    private static void GhiSheetThuChi(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        var sheet = workbook.Worksheets.Add("Thu chi");

        GhiTieuDe(sheet, ["Thời gian", "Loại", "Mã chứng từ", "Nội dung", "Phương thức", "Số tiền"]);

        var ds = duLieu.ThuChi;

        for (var i = 0; i < ds.Count; i++)
        {
            var tc = ds[i];
            var dong = i + 2;

            sheet.Cell(dong, 1).Value = tc.ThoiGian.ToString("dd/MM/yyyy HH:mm");
            sheet.Cell(dong, 2).Value = tc.LoaiHienThi;
            sheet.Cell(dong, 3).Value = tc.MaChungTu;
            sheet.Cell(dong, 4).Value = tc.NoiDung;
            sheet.Cell(dong, 5).Value = tc.PhuongThucHienThi;

            // Số tiền ghi dương, dấu quyết định ở cột "Loại" để SUM(F) đúng.
            GhiTien(sheet.Cell(dong, 6), tc.SoTien);

            sheet.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(dong, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Thu xanh, chi đỏ cho dễ đọc.
            var mau = tc.LaThu ? "#EAF9F5" : "#FDECEC";
            sheet.Range(dong, 1, dong, 6).Style.Fill.BackgroundColor = XLColor.FromHtml(mau);
        }

        // Dòng tổng kết.
        var dongTong = 2 + ds.Count;

        sheet.Cell(dongTong, 4).Value = "Tổng thu";
        sheet.Cell(dongTong, 4).Style.Font.Bold = true;
        GhiTien(sheet.Cell(dongTong, 6), duLieu.SoQuyTongHop.TongThu);
        sheet.Cell(dongTong, 6).Style.Font.Bold = true;

        var dongTong2 = dongTong + 1;

        sheet.Cell(dongTong2, 4).Value = "Tổng chi";
        sheet.Cell(dongTong2, 4).Style.Font.Bold = true;
        GhiTien(sheet.Cell(dongTong2, 6), duLieu.SoQuyTongHop.TongChi);
        sheet.Cell(dongTong2, 6).Style.Font.Bold = true;

        var dongTong3 = dongTong2 + 1;

        sheet.Cell(dongTong3, 4).Value = "Chênh lệch (thu − chi)";
        sheet.Cell(dongTong3, 4).Style.Font.Bold = true;
        GhiTien(sheet.Cell(dongTong3, 6), duLieu.SoQuyTongHop.ChenhLech);
        sheet.Cell(dongTong3, 6).Style.Font.Bold = true;
        sheet.Range(dongTong, 4, dongTong3, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6F5");

        ChinhDoRongCot(sheet, 6, dongTong3, 14, 40);

        // Dòng tổng thu/chi/lệch đã chiếm dongTong..dongTong3 nên thông báo nằm
        // ở dòng kế tiếp, tránh ghi đè số liệu tổng.
        if (ds.Count == 0)
        {
            var dong = dongTong3 + 1;
            sheet.Cell(dong, 1).Value = "Không có phát sinh thu chi trong ngày này.";
            sheet.Range(dong, 1, dong, 6).Merge();
            sheet.Cell(dong, 1).Style.Font.Italic = true;
        }
    }

    // ============================================================
    // SHEET 5 — CÔNG NỢ KHÁCH HÀNG
    // ============================================================

    private static void GhiSheetCongNo(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        var sheet = workbook.Worksheets.Add("Công nợ khách hàng");

        GhiTieuDe(sheet, ["Khách hàng", "Mã hóa đơn", "Thời gian", "Doanh thu", "Đã trả", "Còn nợ", "Trạng thái"]);

        var ds = duLieu.CongNoPhatSinh;

        for (var i = 0; i < ds.Count; i++)
        {
            var cn = ds[i];
            var dong = i + 2;

            sheet.Cell(dong, 1).Value = cn.TenKhachHang;
            sheet.Cell(dong, 2).Value = cn.MaHoaDon;
            GhiNgay(sheet.Cell(dong, 3), cn.ThoiGian);

            GhiTien(sheet.Cell(dong, 4), cn.DoanhThu);
            GhiTien(sheet.Cell(dong, 5), cn.DaTra);
            GhiTien(sheet.Cell(dong, 6), cn.ConNo);

            sheet.Cell(dong, 7).Value = cn.TrangThai;

            sheet.Cell(dong, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Range(dong, 1, dong, 7).Style.Fill.BackgroundColor =
                XLColor.FromHtml(cn.DaThanhToan ? "#EAF9F5" : "#FFF4E5");
        }

        var dongTong = 2 + ds.Count;
        var tong = duLieu.CongNoTongHop;

        sheet.Cell(dongTong, 1).Value = "TỔNG CỘNG";
        sheet.Cell(dongTong, 1).Style.Font.Bold = true;
        GhiTien(sheet.Cell(dongTong, 4), tong.TongCongNoPhatSinh);
        GhiTien(sheet.Cell(dongTong, 5), tong.TongDaTra);
        GhiTien(sheet.Cell(dongTong, 6), tong.TongConNo);
        sheet.Range(dongTong, 1, dongTong, 7).Style.Font.Bold = true;
        sheet.Range(dongTong, 1, dongTong, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6F5");
        sheet.Range(dongTong, 1, dongTong, 7).Style.Border.TopBorder = XLBorderStyleValues.Double;

        ChinhDoRongCot(sheet, 7, dongTong, 14, 34);

        // Dòng tổng đã ghi ở dongTong nên thông báo nằm ở dòng kế tiếp.
        if (ds.Count == 0)
        {
            var dong = dongTong + 1;
            sheet.Cell(dong, 1).Value = "Không có hóa đơn phát sinh trong ngày này.";
            sheet.Range(dong, 1, dong, 7).Merge();
            sheet.Cell(dong, 1).Style.Font.Italic = true;
        }
    }

    // ============================================================
    // SHEET 6 — NHẬP HÀNG
    // ============================================================

    private static void GhiSheetNhapHang(XLWorkbook workbook, BaoCaoCuoiNgayDuLieu duLieu)
    {
        var sheet = workbook.Worksheets.Add("Nhập hàng");

        var nhap = duLieu.NhapHang;

        GhiTieuDe(sheet, ["Chỉ tiêu", "Giá trị", "Ghi chú"]);

        var dong = 2;

        GhiDongGiaTri(sheet, dong++, "Số phiếu nhập", nhap.SoPhieu);
        GhiDongGiaTri(sheet, dong++, "Tổng tiền nhập", nhap.TongTienNhap, true);
        GhiDongGiaTri(sheet, dong++, "Đã trả nhà cung cấp", nhap.DaThanhToan, true);
        GhiDongGiaTri(sheet, dong++, "Còn nợ nhà cung cấp", nhap.ConNo, true);

        sheet.Cell(2, 3).Value = "Tính từ phieu_nhap_hang, loại trừ trạng thái HUY";
        sheet.Cell(dong - 1, 3).Value = "Đã trả + còn nợ = tổng tiền nhập";

        // Dòng doanh thu theo giờ để đối chiếu nhanh trong cùng file.
        dong += 1;
        GhiDongTieuDeNhom(sheet, dong++, "DOANH THU THEO GIỜ");

        GhiTieuDePhu(sheet, dong, "Giờ", "Doanh thu", "Số hóa đơn");
        dong++;

        foreach (var p in duLieu.DoanhThuTheoGio)
        {
            sheet.Cell(dong, 1).Value = p.GioHienThi;
            sheet.Cell(dong, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            GhiTien(sheet.Cell(dong, 2), p.DoanhThu);
            sheet.Cell(dong, 3).Value = p.SoHoaDon;
            sheet.Cell(dong, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            dong++;
        }

        sheet.Column(1).Width = 30;
        sheet.Column(2).Width = 22;
        sheet.Column(3).Width = 46;
    }

    /// <summary>Ghi một dòng tiêu đề phụ (dùng cho bảng lồng bên trong một sheet).</summary>
    private static void GhiTieuDePhu(IXLWorksheet sheet, int dong, string a, string b, string c)
    {
        var o = sheet.Cell(dong, 1);
        o.Value = a;
        o.Style.Font.Bold = true;
        o.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6F5");

        var o2 = sheet.Cell(dong, 2);
        o2.Value = b;
        o2.Style.Font.Bold = true;
        o2.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6F5");

        var o3 = sheet.Cell(dong, 3);
        o3.Value = c;
        o3.Style.Font.Bold = true;
        o3.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6F5");
    }
}
