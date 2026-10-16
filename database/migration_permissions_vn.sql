-- =====================================================================
-- Migration: chuyển hệ thống phân quyền sang mã quyền tiếng Việt
--   <menu>.<chuc_nang>  (ví dụ: banhang.thanh_toan)
--
-- Cách chạy (một lần):
--   mysql -u <user> -p <ten_db> < migration_permissions_vn.sql
--
-- Script idempotent: chạy lại nhiều lần không gây lỗi, không nhân đôi dữ liệu.
-- Bảng quyen tạo bởi migration_permissions.sql / migration_permissions_detail.sql.
-- =====================================================================

SET NAMES utf8mb4;

-- ---------------------------------------------------------------------
-- 0. Bảng quyen (phòng trường hợp chưa chạy migration trước đó)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `quyen` (
  `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_quyen` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_quyen` VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nhom` VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai` VARCHAR(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `mo_ta` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thu_tu` INT(10) UNSIGNED DEFAULT 0,
  `trang_thai` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_quyen_ma_quyen` (`ma_quyen`),
  KEY `idx_quyen_nhom` (`nhom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------
-- 1. Chèn/đồng bộ toàn bộ mã quyền mới
--    (đồng bộ tên/ nhóm/ thứ tự cho khớp danh mục trong ứng dụng)
-- ---------------------------------------------------------------------
INSERT INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, thu_tu, trang_thai) VALUES
-- Tổng quan
('tongquan.xem', 'Xem màn hình tổng quan', 'Dashboard', 'xem', 1, 1),
-- Bán hàng
('banhang.xem', 'Xem màn hình bán hàng', 'Sales', 'xem', 1, 1),
('banhang.tim_san_pham', 'Tìm kiếm sản phẩm', 'Sales', 'tim_san_pham', 2, 1),
('banhang.them_san_pham', 'Thêm sản phẩm vào đơn', 'Sales', 'them_san_pham', 3, 1),
('banhang.sua_so_luong', 'Sửa số lượng', 'Sales', 'sua_so_luong', 4, 1),
('banhang.xoa_san_pham', 'Xóa sản phẩm khỏi đơn', 'Sales', 'xoa_san_pham', 5, 1),
('banhang.giam_gia', 'Giảm giá', 'Sales', 'giam_gia', 6, 1),
('banhang.thanh_toan', 'Thanh toán', 'Sales', 'thanh_toan', 7, 1),
('banhang.in_hoa_don', 'In hóa đơn', 'Sales', 'in_hoa_don', 8, 1),
('banhang.huy_hoa_don', 'Hủy hóa đơn', 'Sales', 'huy_hoa_don', 9, 1),
('banhang.xem_lich_su', 'Xem lịch sử bán hàng', 'Sales', 'xem_lich_su', 10, 1),
-- Hàng hóa
('hanghoa.xem', 'Xem danh sách hàng hóa', 'Products', 'xem', 1, 1),
('hanghoa.xem_chi_tiet', 'Xem chi tiết hàng hóa', 'Products', 'xem_chi_tiet', 2, 1),
('hanghoa.them', 'Thêm hàng hóa', 'Products', 'them', 3, 1),
('hanghoa.sua', 'Sửa hàng hóa', 'Products', 'sua', 4, 1),
('hanghoa.xoa', 'Xóa hàng hóa', 'Products', 'xoa', 5, 1),
('hanghoa.import', 'Import Excel', 'Products', 'import', 6, 1),
('hanghoa.export', 'Export Excel', 'Products', 'export', 7, 1),
('hanghoa.dieu_chinh_ton_kho', 'Điều chỉnh tồn kho', 'Products', 'dieu_chinh_ton_kho', 8, 1),
('hanghoa.sua_gia_ban', 'Sửa giá bán', 'Products', 'sua_gia_ban', 9, 1),
('hanghoa.sua_gia_von', 'Sửa giá vốn', 'Products', 'sua_gia_von', 10, 1),
-- Hóa đơn
('hoadon.xem', 'Xem hóa đơn', 'Invoices', 'xem', 1, 1),
('hoadon.xem_chi_tiet', 'Xem chi tiết hóa đơn', 'Invoices', 'xem_chi_tiet', 2, 1),
('hoadon.tao', 'Tạo hóa đơn', 'Invoices', 'tao', 3, 1),
('hoadon.sua', 'Sửa hóa đơn', 'Invoices', 'sua', 4, 1),
('hoadon.huy', 'Hủy hóa đơn', 'Invoices', 'huy', 5, 1),
('hoadon.xoa', 'Xóa hóa đơn', 'Invoices', 'xoa', 6, 1),
('hoadon.in', 'In hóa đơn', 'Invoices', 'in', 7, 1),
('hoadon.import', 'Import hóa đơn', 'Invoices', 'import', 8, 1),
('hoadon.export', 'Export hóa đơn', 'Invoices', 'export', 9, 1),
-- Khách hàng
('khachhang.xem', 'Xem khách hàng', 'Customers', 'xem', 1, 1),
('khachhang.xem_chi_tiet', 'Xem chi tiết khách hàng', 'Customers', 'xem_chi_tiet', 2, 1),
('khachhang.them', 'Thêm khách hàng', 'Customers', 'them', 3, 1),
('khachhang.sua', 'Sửa khách hàng', 'Customers', 'sua', 4, 1),
('khachhang.xoa', 'Xóa khách hàng', 'Customers', 'xoa', 5, 1),
('khachhang.import', 'Import khách hàng', 'Customers', 'import', 6, 1),
('khachhang.export', 'Export khách hàng', 'Customers', 'export', 7, 1),
('khachhang.xem_cong_no', 'Xem công nợ', 'Customers', 'xem_cong_no', 8, 1),
('khachhang.thu_tien', 'Thu tiền khách hàng', 'Customers', 'thu_tien', 9, 1),
-- Nhà cung cấp
('nhacungcap.xem', 'Xem nhà cung cấp', 'Suppliers', 'xem', 1, 1),
('nhacungcap.xem_chi_tiet', 'Xem chi tiết nhà cung cấp', 'Suppliers', 'xem_chi_tiet', 2, 1),
('nhacungcap.them', 'Thêm nhà cung cấp', 'Suppliers', 'them', 3, 1),
('nhacungcap.sua', 'Sửa nhà cung cấp', 'Suppliers', 'sua', 4, 1),
('nhacungcap.xoa', 'Xóa nhà cung cấp', 'Suppliers', 'xoa', 5, 1),
('nhacungcap.import', 'Import nhà cung cấp', 'Suppliers', 'import', 6, 1),
('nhacungcap.export', 'Export nhà cung cấp', 'Suppliers', 'export', 7, 1),
('nhacungcap.xem_cong_no', 'Xem công nợ', 'Suppliers', 'xem_cong_no', 8, 1),
('nhacungcap.thanh_toan', 'Thanh toán công nợ', 'Suppliers', 'thanh_toan', 9, 1),
-- Phiếu nhập hàng
('nhaphang.xem', 'Xem phiếu nhập hàng', 'Purchases', 'xem', 1, 1),
('nhaphang.xem_chi_tiet', 'Xem chi tiết phiếu nhập', 'Purchases', 'xem_chi_tiet', 2, 1),
('nhaphang.tao', 'Tạo phiếu nhập', 'Purchases', 'tao', 3, 1),
('nhaphang.sua', 'Sửa phiếu nhập', 'Purchases', 'sua', 4, 1),
('nhaphang.xoa', 'Xóa phiếu nhập', 'Purchases', 'xoa', 5, 1),
('nhaphang.import', 'Import phiếu nhập', 'Purchases', 'import', 6, 1),
('nhaphang.export', 'Export phiếu nhập', 'Purchases', 'export', 7, 1),
('nhaphang.in', 'In phiếu nhập', 'Purchases', 'in', 8, 1),
-- Sổ quỹ
('soquy.xem', 'Xem sổ quỹ', 'CashBook', 'xem', 1, 1),
('soquy.them_phieu_thu', 'Thêm phiếu thu', 'CashBook', 'them_phieu_thu', 2, 1),
('soquy.them_phieu_chi', 'Thêm phiếu chi', 'CashBook', 'them_phieu_chi', 3, 1),
('soquy.sua_phieu', 'Sửa phiếu', 'CashBook', 'sua_phieu', 4, 1),
('soquy.xoa_phieu', 'Xóa phiếu', 'CashBook', 'xoa_phieu', 5, 1),
('soquy.huy_phieu', 'Hủy phiếu', 'CashBook', 'huy_phieu', 6, 1),
('soquy.xem_so_du', 'Xem số dư', 'CashBook', 'xem_so_du', 7, 1),
('soquy.khoi_tao', 'Khởi tạo sổ quỹ', 'CashBook', 'khoi_tao', 8, 1),
('soquy.so_du_dau_ky', 'Nhập số dư đầu kỳ', 'CashBook', 'so_du_dau_ky', 9, 1),
('soquy.xem_chi_tiet', 'Xem chi tiết phiếu', 'CashBook', 'xem_chi_tiet', 10, 1),
('soquy.import', 'Import sổ quỹ', 'CashBook', 'import', 11, 1),
('soquy.export', 'Export sổ quỹ', 'CashBook', 'export', 12, 1),
-- Báo cáo
('baocao.xem', 'Xem báo cáo', 'Reports', 'xem', 1, 1),
('baocao.bao_cao_cuoi_ngay', 'Báo cáo cuối ngày', 'Reports', 'bao_cao_cuoi_ngay', 2, 1),
('baocao.bao_cao_ban_hang', 'Báo cáo bán hàng', 'Reports', 'bao_cao_ban_hang', 3, 1),
('baocao.bao_cao_hang_hoa', 'Báo cáo hàng hóa', 'Reports', 'bao_cao_hang_hoa', 4, 1),
('baocao.bao_cao_phieu_nhap', 'Báo cáo phiếu nhập hàng', 'Reports', 'bao_cao_phieu_nhap', 5, 1),
('baocao.xem_doanh_thu', 'Xem doanh thu', 'Reports', 'xem_doanh_thu', 6, 1),
('baocao.xem_loi_nhuan', 'Xem lợi nhuận', 'Reports', 'xem_loi_nhuan', 7, 1),
('baocao.xem_gia_von', 'Xem giá vốn', 'Reports', 'xem_gia_von', 8, 1),
('baocao.cong_no_khach_hang', 'Xem công nợ khách hàng', 'Reports', 'cong_no_khach_hang', 9, 1),
('baocao.cong_no_nha_cung_cap', 'Xem công nợ nhà cung cấp', 'Reports', 'cong_no_nha_cung_cap', 10, 1),
('baocao.dong_tien', 'Xem dòng tiền', 'Reports', 'dong_tien', 11, 1),
('baocao.in', 'In báo cáo', 'Reports', 'in', 12, 1),
('baocao.export', 'Export báo cáo', 'Reports', 'export', 13, 1),
-- Tài khoản
('taikhoan.xem', 'Xem tài khoản', 'Users', 'xem', 1, 1),
('taikhoan.them', 'Thêm tài khoản', 'Users', 'them', 2, 1),
('taikhoan.sua', 'Sửa tài khoản', 'Users', 'sua', 3, 1),
('taikhoan.khoa', 'Khóa tài khoản', 'Users', 'khoa', 4, 1),
('taikhoan.mo_khoa', 'Mở khóa tài khoản', 'Users', 'mo_khoa', 5, 1),
('taikhoan.xoa', 'Xóa tài khoản', 'Users', 'xoa', 6, 1),
('taikhoan.phan_quyen', 'Phân quyền tài khoản', 'Users', 'phan_quyen', 7, 1),
('taikhoan.dat_lai_mat_khau', 'Đặt lại mật khẩu', 'Users', 'dat_lai_mat_khau', 8, 1),
('taikhoan.doi_mat_khau', 'Tự đổi mật khẩu', 'Users', 'doi_mat_khau', 9, 1),
-- Cấu hình hệ thống
('caidat.xem', 'Xem cấu hình hệ thống', 'Settings', 'xem', 1, 1),
('caidat.sua', 'Cấu hình hệ thống', 'Settings', 'sua', 2, 1),
('thongtin.xem', 'Xem thông tin tiệm', 'Settings', 'xem_thong_tin', 3, 1),
('thongtin.sua', 'Sửa thông tin tiệm', 'Settings', 'sua_thong_tin', 4, 1)
ON DUPLICATE KEY UPDATE
  ten_quyen = VALUES(ten_quyen),
  nhom = VALUES(nhom),
  loai = VALUES(loai),
  thu_tu = VALUES(thu_tu),
  trang_thai = 1;

-- ---------------------------------------------------------------------
-- 2. Bảng ánh xạ mã quyền cũ -> mã quyền mới
-- ---------------------------------------------------------------------
DROP TEMPORARY TABLE IF EXISTS `_map_quyen_vn`;
CREATE TEMPORARY TABLE `_map_quyen_vn` (
  old_ma VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL PRIMARY KEY,
  new_ma VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `_map_quyen_vn` (old_ma, new_ma) VALUES
-- Dashboard
('Dashboard.View', 'tongquan.xem'),
-- Sales
('Sales.View', 'banhang.xem'),
('Sales.Create', 'banhang.thanh_toan'),
('Sales.Payment', 'banhang.thanh_toan'),
('Sales.Edit', 'banhang.sua_so_luong'),
('Sales.Delete', 'banhang.xoa_san_pham'),
('Sales.History', 'banhang.xem_lich_su'),
-- Products
('Products.View', 'hanghoa.xem'),
('Products.Detail', 'hanghoa.xem_chi_tiet'),
('Products.Create', 'hanghoa.them'),
('Products.Edit', 'hanghoa.sua'),
('Products.Delete', 'hanghoa.xoa'),
('Products.Import', 'hanghoa.import'),
('Products.Export', 'hanghoa.export'),
('Products.AdjustStock', 'hanghoa.dieu_chinh_ton_kho'),
('Products.UpdatePrice', 'hanghoa.sua_gia_ban'),
('Products.UpdateCost', 'hanghoa.sua_gia_von'),
-- Purchases
('Purchases.View', 'nhaphang.xem'),
('Purchases.Detail', 'nhaphang.xem_chi_tiet'),
('Purchases.Create', 'nhaphang.tao'),
('Purchases.Edit', 'nhaphang.sua'),
('Purchases.Delete', 'nhaphang.xoa'),
('Purchases.Import', 'nhaphang.import'),
('Purchases.Export', 'nhaphang.export'),
('Purchases.Print', 'nhaphang.in'),
-- Invoices
('Invoices.View', 'hoadon.xem'),
('Invoices.Detail', 'hoadon.xem_chi_tiet'),
('Invoices.Create', 'hoadon.tao'),
('Invoices.Edit', 'hoadon.sua'),
('Invoices.Delete', 'hoadon.xoa'),
('Invoices.Cancel', 'hoadon.huy'),
('Invoices.Print', 'hoadon.in'),
('Invoices.Import', 'hoadon.import'),
('Invoices.Export', 'hoadon.export'),
-- Customers
('Customers.View', 'khachhang.xem'),
('Customers.Detail', 'khachhang.xem_chi_tiet'),
('Customers.Create', 'khachhang.them'),
('Customers.Edit', 'khachhang.sua'),
('Customers.Delete', 'khachhang.xoa'),
('Customers.Import', 'khachhang.import'),
('Customers.Export', 'khachhang.export'),
('Customers.DebtView', 'khachhang.xem_cong_no'),
('Customers.DebtPay', 'khachhang.thu_tien'),
-- Suppliers
('Suppliers.View', 'nhacungcap.xem'),
('Suppliers.Detail', 'nhacungcap.xem_chi_tiet'),
('Suppliers.Create', 'nhacungcap.them'),
('Suppliers.Edit', 'nhacungcap.sua'),
('Suppliers.Delete', 'nhacungcap.xoa'),
('Suppliers.Import', 'nhacungcap.import'),
('Suppliers.Export', 'nhacungcap.export'),
('Suppliers.DebtView', 'nhacungcap.xem_cong_no'),
('Suppliers.DebtPay', 'nhacungcap.thanh_toan'),
-- CashBook
('CashBook.View', 'soquy.xem'),
('CashBook.CreateReceipt', 'soquy.them_phieu_thu'),
('CashBook.CreatePayment', 'soquy.them_phieu_chi'),
('CashBook.Edit', 'soquy.sua_phieu'),
('CashBook.Delete', 'soquy.xoa_phieu'),
('CashBook.Cancel', 'soquy.huy_phieu'),
('CashBook.Init', 'soquy.khoi_tao'),
('CashBook.OpeningBalance', 'soquy.so_du_dau_ky'),
('CashBook.Detail', 'soquy.xem_chi_tiet'),
('CashBook.Import', 'soquy.import'),
('CashBook.Export', 'soquy.export'),
-- Reports
('Reports.View', 'baocao.xem'),
('Reports.Sales', 'baocao.bao_cao_ban_hang'),
('Reports.Purchases', 'baocao.bao_cao_phieu_nhap'),
('Reports.Stock', 'baocao.bao_cao_hang_hoa'),
('Reports.CustomerDebt', 'baocao.cong_no_khach_hang'),
('Reports.SupplierDebt', 'baocao.cong_no_nha_cung_cap'),
('Reports.CashFlow', 'baocao.dong_tien'),
('Reports.Print', 'baocao.in'),
('Reports.Export', 'baocao.export'),
-- Users
('Users.View', 'taikhoan.xem'),
('Users.Create', 'taikhoan.them'),
('Users.Edit', 'taikhoan.sua'),
('Users.Lock', 'taikhoan.khoa'),
('Users.Unlock', 'taikhoan.mo_khoa'),
('Users.Delete', 'taikhoan.xoa'),
('Users.AssignPermission', 'taikhoan.phan_quyen'),
('Users.ResetPassword', 'taikhoan.dat_lai_mat_khau'),
('Users.ChangePassword', 'taikhoan.doi_mat_khau'),
-- Settings
('Settings.View', 'caidat.xem'),
('Settings.Edit', 'caidat.sua');

-- ---------------------------------------------------------------------
-- 3. Chuyển phân quyền vai trò từ mã cũ sang mã mới
-- ---------------------------------------------------------------------
INSERT IGNORE INTO `vai_tro_quyen` (vai_tro_id, quyen_id)
SELECT DISTINCT vtq.vai_tro_id, qnew.id
FROM `vai_tro_quyen` vtq
JOIN `quyen` qold ON qold.id = vtq.quyen_id
JOIN `_map_quyen_vn` m ON m.old_ma = qold.ma_quyen
JOIN `quyen` qnew ON qnew.ma_quyen = m.new_ma;

-- ---------------------------------------------------------------------
-- 4. Chuyển phân quyền tài khoản (override) từ mã cũ sang mã mới
-- ---------------------------------------------------------------------
INSERT IGNORE INTO `nguoi_dung_quyen` (nguoi_dung_id, quyen_id, cho_phep, nguoi_cap_nhat_id)
SELECT DISTINCT ndq.nguoi_dung_id, qnew.id, ndq.cho_phep, ndq.nguoi_cap_nhat_id
FROM `nguoi_dung_quyen` ndq
JOIN `quyen` qold ON qold.id = ndq.quyen_id
JOIN `_map_quyen_vn` m ON m.old_ma = qold.ma_quyen
JOIN `quyen` qnew ON qnew.ma_quyen = m.new_ma;

-- ---------------------------------------------------------------------
-- 5. QUAN_TRI luôn có toàn bộ quyền
-- ---------------------------------------------------------------------
INSERT IGNORE INTO `vai_tro_quyen` (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM `vai_tro` vt
CROSS JOIN `quyen` q
WHERE vt.ma_vai_tro = 'QUAN_TRI';

-- ---------------------------------------------------------------------
-- 6. Gán nhóm quyền mặc định cho các vai trò còn lại
--    (vai trò chỉ là nhóm mặc định, quản trị vẫn sửa lại từng tài khoản được)
-- ---------------------------------------------------------------------
-- QUAN_LY
INSERT IGNORE INTO `vai_tro_quyen` (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM `vai_tro` vt
JOIN `quyen` q ON q.ma_quyen IN (
  'tongquan.xem',
  'banhang.xem','banhang.tim_san_pham','banhang.them_san_pham','banhang.sua_so_luong',
  'banhang.xoa_san_pham','banhang.giam_gia','banhang.thanh_toan','banhang.in_hoa_don','banhang.xem_lich_su',
  'hanghoa.xem','hanghoa.xem_chi_tiet','hanghoa.them','hanghoa.sua','hanghoa.xoa',
  'hanghoa.import','hanghoa.export','hanghoa.dieu_chinh_ton_kho','hanghoa.sua_gia_ban',
  'hoadon.xem','hoadon.xem_chi_tiet','hoadon.tao','hoadon.sua','hoadon.huy','hoadon.in','hoadon.export',
  'khachhang.xem','khachhang.xem_chi_tiet','khachhang.them','khachhang.sua','khachhang.xoa',
  'khachhang.import','khachhang.export','khachhang.xem_cong_no','khachhang.thu_tien',
  'nhacungcap.xem','nhacungcap.xem_chi_tiet','nhacungcap.them','nhacungcap.sua','nhacungcap.xoa',
  'nhacungcap.import','nhacungcap.export','nhacungcap.xem_cong_no','nhacungcap.thanh_toan',
  'nhaphang.xem','nhaphang.xem_chi_tiet','nhaphang.tao','nhaphang.sua','nhaphang.xoa',
  'nhaphang.import','nhaphang.export','nhaphang.in',
  'soquy.xem','soquy.them_phieu_thu','soquy.them_phieu_chi','soquy.sua_phieu','soquy.xoa_phieu',
  'soquy.huy_phieu','soquy.xem_so_du','soquy.import','soquy.export',
  'baocao.xem','baocao.bao_cao_cuoi_ngay','baocao.bao_cao_ban_hang','baocao.bao_cao_hang_hoa',
  'baocao.bao_cao_phieu_nhap','baocao.xem_doanh_thu','baocao.xem_loi_nhuan','baocao.xem_gia_von',
  'baocao.in','baocao.export',
  'taikhoan.doi_mat_khau',
  'caidat.xem','caidat.sua','thongtin.xem','thongtin.sua'
)
WHERE vt.ma_vai_tro = 'QUAN_LY';

-- THU_NGAN
INSERT IGNORE INTO `vai_tro_quyen` (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM `vai_tro` vt
JOIN `quyen` q ON q.ma_quyen IN (
  'tongquan.xem',
  'banhang.xem','banhang.tim_san_pham','banhang.them_san_pham','banhang.sua_so_luong',
  'banhang.xoa_san_pham','banhang.thanh_toan','banhang.in_hoa_don',
  'hanghoa.xem','hanghoa.xem_chi_tiet',
  'hoadon.xem','hoadon.xem_chi_tiet','hoadon.tao','hoadon.in',
  'khachhang.xem','khachhang.xem_chi_tiet','khachhang.them','khachhang.sua',
  'khachhang.xem_cong_no','khachhang.thu_tien',
  'soquy.xem','soquy.them_phieu_thu','soquy.them_phieu_chi','soquy.xem_so_du','soquy.xem_chi_tiet',
  'baocao.xem','baocao.bao_cao_cuoi_ngay','baocao.bao_cao_ban_hang','baocao.in',
  'taikhoan.doi_mat_khau'
)
WHERE vt.ma_vai_tro = 'THU_NGAN';

-- KHO
INSERT IGNORE INTO `vai_tro_quyen` (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM `vai_tro` vt
JOIN `quyen` q ON q.ma_quyen IN (
  'tongquan.xem',
  'hanghoa.xem','hanghoa.xem_chi_tiet','hanghoa.them','hanghoa.sua','hanghoa.xoa',
  'hanghoa.import','hanghoa.export','hanghoa.dieu_chinh_ton_kho','hanghoa.sua_gia_ban','hanghoa.sua_gia_von',
  'nhaphang.xem','nhaphang.xem_chi_tiet','nhaphang.tao','nhaphang.sua','nhaphang.xoa',
  'nhaphang.import','nhaphang.export','nhaphang.in',
  'nhacungcap.xem','nhacungcap.xem_chi_tiet','nhacungcap.them','nhacungcap.sua',
  'nhacungcap.import','nhacungcap.export','nhacungcap.xem_cong_no',
  'baocao.xem','baocao.bao_cao_hang_hoa','baocao.bao_cao_phieu_nhap','baocao.xem_gia_von','baocao.in',
  'taikhoan.doi_mat_khau'
)
WHERE vt.ma_vai_tro = 'KHO';

-- BAO_CAO
INSERT IGNORE INTO `vai_tro_quyen` (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM `vai_tro` vt
JOIN `quyen` q ON q.ma_quyen IN (
  'tongquan.xem',
  'baocao.xem','baocao.bao_cao_cuoi_ngay','baocao.bao_cao_ban_hang','baocao.bao_cao_hang_hoa',
  'baocao.bao_cao_phieu_nhap','baocao.xem_doanh_thu','baocao.xem_loi_nhuan','baocao.xem_gia_von',
  'baocao.cong_no_khach_hang','baocao.cong_no_nha_cung_cap','baocao.dong_tien','baocao.in','baocao.export',
  'taikhoan.doi_mat_khau'
)
WHERE vt.ma_vai_tro = 'BAO_CAO';

-- ---------------------------------------------------------------------
-- 7. Dọn mã quyền cũ (FK sẽ tự xóa phân quyền tham chiếu mã cũ)
-- ---------------------------------------------------------------------
DELETE FROM `quyen` WHERE ma_quyen IN (SELECT old_ma FROM `_map_quyen_vn`);

DROP TEMPORARY TABLE IF EXISTS `_map_quyen_vn`;
