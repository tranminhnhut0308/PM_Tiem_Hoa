-- Mở rộng quyền chi tiết theo từng chức năng (bổ sung cho migration_permissions.sql)
-- Bảng/model/service permission tồn tại sẵn, script này chỉ INSERT thêm các quyền còn thiếu.

-- 1. Dashboard: bổ sung nhóm tổng quan
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Dashboard.View', 'Xem Tổng quan', 'Dashboard', 'View', 'Được xem màn hình tổng quan', 1);

-- 2. Products: chi tiết / giá vốn / giá bán / tồn kho
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Products.Detail', 'Xem chi tiết Hàng hóa', 'Products', 'View', 'Mở xem chi tiết 1 hàng hóa', 16),
('Products.UpdateCost', 'Cập nhật giá vốn', 'Products', 'EditCost', 'Sửa giá nhập / giá vốn của hàng hóa', 17),
('Products.UpdatePrice', 'Cập nhật giá bán', 'Products', 'EditPrice', 'Sửa giá bán của hàng hóa', 18),
('Products.AdjustStock', 'Điều chỉnh tồn kho', 'Products', 'AdjustStock', 'Điều chỉnh số lượng tồn kho', 19);

-- 3. Purchases: chi tiết / in phiếu
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Purchases.Detail', 'Xem chi tiết Phiếu nhập', 'Purchases', 'View', 'Mở xem chi tiết 1 phiếu nhập', 26),
('Purchases.Print', 'In Phiếu nhập', 'Purchases', 'Print', 'In phiếu nhập hàng', 27);

-- 4. Invoices: in hóa đơn
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Invoices.Print', 'In Hóa đơn', 'Invoices', 'Print', 'In hóa đơn bán hàng', 37);

-- 5. Customers: chi tiết / import / công nợ
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Customers.Detail', 'Xem chi tiết Khách hàng', 'Customers', 'View', 'Mở xem chi tiết 1 khách hàng', 45),
('Customers.Import', 'Import Khách hàng', 'Customers', 'Import', 'Import khách hàng từ file', 46),
('Customers.DebtView', 'Xem công nợ Khách hàng', 'Customers', 'DebtView', 'Xem công nợ của khách hàng', 47),
('Customers.DebtPay', 'Thu nợ Khách hàng', 'Customers', 'DebtPay', 'Ghi nhận thu nợ của khách hàng', 48);

-- 6. Suppliers: chi tiết / import / công nợ
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Suppliers.Detail', 'Xem chi tiết Nhà cung cấp', 'Suppliers', 'View', 'Mở xem chi tiết 1 nhà cung cấp', 55),
('Suppliers.Import', 'Import Nhà cung cấp', 'Suppliers', 'Import', 'Import nhà cung cấp từ file', 56),
('Suppliers.DebtView', 'Xem công nợ Nhà cung cấp', 'Suppliers', 'DebtView', 'Xem công nợ của nhà cung cấp', 57),
('Suppliers.DebtPay', 'Trả nợ Nhà cung cấp', 'Suppliers', 'DebtPay', 'Ghi nhận trả nợ cho nhà cung cấp', 58);

-- 7. CashBook: khởi tạo / số dư đầu kỳ / xóa / xem chi tiết
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('CashBook.Init', 'Khởi tạo Sổ quỹ', 'CashBook', 'Init', 'Khởi tạo sổ quỹ cho kỳ kế toán', 67),
('CashBook.OpeningBalance', 'Nhập số dư đầu kỳ', 'CashBook', 'OpeningBalance', 'Nhập số dư đầu kỳ của sổ quỹ', 68),
('CashBook.Delete', 'Xóa Phiếu thu/chi', 'CashBook', 'Delete', 'Xóa phiếu thu hoặc phiếu chi', 69),
('CashBook.Detail', 'Xem chi tiết Phiếu thu/chi', 'CashBook', 'View', 'Mở xem chi tiết 1 phiếu thu/chi', 70);

-- 8. Reports: tách từng loại báo cáo
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Reports.Sales', 'Báo cáo doanh thu', 'Reports', 'View', 'Xem báo cáo doanh thu bán hàng', 72),
('Reports.Purchases', 'Báo cáo nhập hàng', 'Reports', 'View', 'Xem báo cáo nhập hàng', 73),
('Reports.Stock', 'Báo cáo tồn kho', 'Reports', 'View', 'Xem báo cáo tồn kho', 74),
('Reports.CustomerDebt', 'Báo cáo công nợ khách hàng', 'Reports', 'View', 'Xem báo cáo công nợ khách hàng', 75),
('Reports.SupplierDebt', 'Báo cáo công nợ nhà cung cấp', 'Reports', 'View', 'Xem báo cáo công nợ nhà cung cấp', 76),
('Reports.CashFlow', 'Báo cáo dòng tiền', 'Reports', 'View', 'Xem báo cáo thu chi dòng tiền', 77),
('Reports.Print', 'In Báo cáo', 'Reports', 'Print', 'In báo cáo', 78);

-- 9. Sales: sửa / xóa / thanh toán / lịch sử
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Sales.Edit', 'Sửa hóa đơn bán hàng', 'Sales', 'Edit', 'Sửa hóa đơn bán hàng đã tạo', 79),
('Sales.Delete', 'Xóa hóa đơn bán hàng', 'Sales', 'Delete', 'Xóa hóa đơn bán hàng', 80),
('Sales.Payment', 'Thanh toán hóa đơn', 'Sales', 'Payment', 'Thanh toán hóa đơn bán hàng', 81),
('Sales.History', 'Xem lịch sử bán hàng', 'Sales', 'View', 'Xem lịch sử bán hàng', 82);

-- 10. Users: tự đổi mật khẩu
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, mo_ta, thu_tu) VALUES
('Users.ChangePassword', 'Tự đổi mật khẩu', 'Users', 'ChangePassword', 'Cho phép người dùng tự đổi mật khẩu của mình', 88);

-- 11. QUAN_TRI phải có TOÀN BỘ quyền (kể cả quyền vừa thêm)
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
CROSS JOIN quyen q
WHERE vt.ma_vai_tro = 'QUAN_TRI';

-- 12. Cấp các quyền chi tiết mới cho role phù hợp
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Products.Detail','Products.UpdateCost','Products.UpdatePrice','Products.AdjustStock',
  'Purchases.Detail','Purchases.Print',
  'Invoices.Print',
  'Customers.Detail','Customers.Import','Customers.DebtView','Customers.DebtPay',
  'Suppliers.Detail','Suppliers.Import','Suppliers.DebtView','Suppliers.DebtPay',
  'CashBook.Init','CashBook.OpeningBalance','CashBook.Delete','CashBook.Detail',
  'Reports.Sales','Reports.Purchases','Reports.Stock','Reports.CustomerDebt','Reports.SupplierDebt','Reports.CashFlow','Reports.Print',
  'Sales.Edit','Sales.Delete','Sales.Payment','Sales.History',
  'Users.ChangePassword'
)
WHERE vt.ma_vai_tro = 'QUAN_LY';

-- THU_NGAN: bán hàng + thu nợ + công nợ khách
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Sales.Edit','Sales.Payment','Sales.History',
  'Invoices.Print',
  'Customers.Detail','Customers.DebtView','Customers.DebtPay',
  'CashBook.Detail','CashBook.Edit',
  'Reports.Sales','Reports.Print',
  'Users.ChangePassword'
)
WHERE vt.ma_vai_tro = 'THU_NGAN';

-- KHO: giá vốn + tồn kho + in phiếu nhập + công nợ nhà cung cấp
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Products.Detail','Products.UpdateCost','Products.UpdatePrice','Products.AdjustStock',
  'Purchases.Detail','Purchases.Print',
  'Suppliers.Detail','Suppliers.DebtView','Suppliers.DebtPay',
  'Reports.Purchases','Reports.Stock','Reports.SupplierDebt','Reports.Print',
  'Users.ChangePassword'
)
WHERE vt.ma_vai_tro = 'KHO';

-- BAO_CAO: các báo cáo chi tiết
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Reports.Sales','Reports.Purchases','Reports.Stock','Reports.CustomerDebt','Reports.SupplierDebt','Reports.CashFlow','Reports.Print',
  'Users.ChangePassword'
)
WHERE vt.ma_vai_tro = 'BAO_CAO';
