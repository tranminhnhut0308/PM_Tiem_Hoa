-- Bổ sung hệ thống phân quyền chi tiết (tương thích với schema hiện tại)

-- 1. Bảng quyền (permissions)
CREATE TABLE IF NOT EXISTS `quyen` (
  `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_quyen` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Key quyền: Products.View, Users.Create,...',
  `ten_quyen` VARCHAR(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên hiển thị',
  `nhom` VARCHAR(50) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Module: Dashboard, Products, Invoices, Customers, Suppliers, CashBook, Reports, Users, Settings, Sales, Purchases',
  `loai` VARCHAR(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'View, Create, Edit, Delete, Import, Export, Lock, Unlock, ResetPassword, AssignPermission, Cancel, ManageRoles',
  `mo_ta` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thu_tu` INT(10) UNSIGNED DEFAULT 0,
  `trang_thai` TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_quyen_ma_quyen` (`ma_quyen`),
  KEY `idx_quyen_nhom` (`nhom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. Quyền theo vai trò
CREATE TABLE IF NOT EXISTS `vai_tro_quyen` (
  `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `vai_tro_id` INT(10) UNSIGNED NOT NULL,
  `quyen_id` INT(10) UNSIGNED NOT NULL,
  `nguoi_cap_nhat_id` INT(10) UNSIGNED DEFAULT NULL,
  `ngay_cap_nhat` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_vai_tro_quyen` (`vai_tro_id`, `quyen_id`),
  KEY `fk_vtq_quyen` (`quyen_id`),
  KEY `fk_vtq_nguoi_cap_nhat` (`nguoi_cap_nhat_id`),
  CONSTRAINT `fk_vtq_vai_tro` FOREIGN KEY (`vai_tro_id`) REFERENCES `vai_tro` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_vtq_quyen` FOREIGN KEY (`quyen_id`) REFERENCES `quyen` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_vtq_nguoi_cap_nhat` FOREIGN KEY (`nguoi_cap_nhat_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Quyền theo người dùng (override)
CREATE TABLE IF NOT EXISTS `nguoi_dung_quyen` (
  `id` INT(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nguoi_dung_id` INT(10) UNSIGNED NOT NULL,
  `quyen_id` INT(10) UNSIGNED NOT NULL,
  `cho_phep` TINYINT(1) NOT NULL DEFAULT 1,
  `nguoi_cap_nhat_id` INT(10) UNSIGNED DEFAULT NULL,
  `ngay_cap_nhat` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nguoi_dung_quyen` (`nguoi_dung_id`, `quyen_id`),
  KEY `fk_ndq_quyen` (`quyen_id`),
  KEY `fk_ndq_nguoi_cap_nhat` (`nguoi_cap_nhat_id`),
  CONSTRAINT `fk_ndq_nguoi_dung` FOREIGN KEY (`nguoi_dung_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_ndq_quyen` FOREIGN KEY (`quyen_id`) REFERENCES `quyen` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_ndq_nguoi_cap_nhat` FOREIGN KEY (`nguoi_cap_nhat_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Seed permissions theo yêu cầu (checkbox đơn giản)
INSERT IGNORE INTO `quyen` (ma_quyen, ten_quyen, nhom, loai, thu_tu) VALUES
-- Dashboard
('Dashboard.View', 'Xem Tổng quan', 'Dashboard', 'View', 1),
-- Products
('Products.View', 'Xem Hàng hóa', 'Products', 'View', 10),
('Products.Create', 'Thêm Hàng hóa', 'Products', 'Create', 11),
('Products.Edit', 'Sửa Hàng hóa', 'Products', 'Edit', 12),
('Products.Delete', 'Xóa Hàng hóa', 'Products', 'Delete', 13),
('Products.Import', 'Import Hàng hóa', 'Products', 'Import', 14),
('Products.Export', 'Export Hàng hóa', 'Products', 'Export', 15),
-- Purchases
('Purchases.View', 'Xem Nhập hàng', 'Purchases', 'View', 20),
('Purchases.Create', 'Thêm Phiếu nhập', 'Purchases', 'Create', 21),
('Purchases.Edit', 'Sửa Phiếu nhập', 'Purchases', 'Edit', 22),
('Purchases.Delete', 'Xóa Phiếu nhập', 'Purchases', 'Delete', 23),
('Purchases.Import', 'Import Nhập hàng', 'Purchases', 'Import', 24),
('Purchases.Export', 'Export Nhập hàng', 'Purchases', 'Export', 25),
-- Invoices
('Invoices.View', 'Xem Hóa đơn', 'Invoices', 'View', 30),
('Invoices.Create', 'Thêm Hóa đơn', 'Invoices', 'Create', 31),
('Invoices.Edit', 'Sửa Hóa đơn', 'Invoices', 'Edit', 32),
('Invoices.Delete', 'Xóa Hóa đơn', 'Invoices', 'Delete', 33),
('Invoices.Import', 'Import Hóa đơn', 'Invoices', 'Import', 34),
('Invoices.Export', 'Export Hóa đơn', 'Invoices', 'Export', 35),
('Invoices.Cancel', 'Hủy Hóa đơn', 'Invoices', 'Cancel', 36),
-- Customers
('Customers.View', 'Xem Khách hàng', 'Customers', 'View', 40),
('Customers.Create', 'Thêm Khách hàng', 'Customers', 'Create', 41),
('Customers.Edit', 'Sửa Khách hàng', 'Customers', 'Edit', 42),
('Customers.Delete', 'Xóa Khách hàng', 'Customers', 'Delete', 43),
('Customers.Export', 'Export Khách hàng', 'Customers', 'Export', 44),
-- Suppliers
('Suppliers.View', 'Xem Nhà cung cấp', 'Suppliers', 'View', 50),
('Suppliers.Create', 'Thêm Nhà cung cấp', 'Suppliers', 'Create', 51),
('Suppliers.Edit', 'Sửa Nhà cung cấp', 'Suppliers', 'Edit', 52),
('Suppliers.Delete', 'Xóa Nhà cung cấp', 'Suppliers', 'Delete', 53),
('Suppliers.Export', 'Export Nhà cung cấp', 'Suppliers', 'Export', 54),
-- CashBook
('CashBook.View', 'Xem Sổ quỹ', 'CashBook', 'View', 60),
('CashBook.CreateReceipt', 'Tạo Phiếu thu', 'CashBook', 'CreateReceipt', 61),
('CashBook.CreatePayment', 'Tạo Phiếu chi', 'CashBook', 'CreatePayment', 62),
('CashBook.Edit', 'Sửa Sổ quỹ', 'CashBook', 'Edit', 63),
('CashBook.Cancel', 'Hủy Phiếu', 'CashBook', 'Cancel', 64),
('CashBook.Import', 'Import Sổ quỹ', 'CashBook', 'Import', 65),
('CashBook.Export', 'Export Sổ quỹ', 'CashBook', 'Export', 66),
-- Reports
('Reports.View', 'Xem Báo cáo', 'Reports', 'View', 70),
('Reports.Export', 'Export Báo cáo', 'Reports', 'Export', 71),
-- Sales (Bán hàng - yêu cầu checkbox)
('Sales.View', 'Xem/Bán hàng', 'Sales', 'View', 75),
('Sales.Create', 'Bán hàng', 'Sales', 'Create', 76),
-- Users
('Users.View', 'Xem Quản lý tài khoản', 'Users', 'View', 80),
('Users.Create', 'Tạo tài khoản', 'Users', 'Create', 81),
('Users.Edit', 'Sửa tài khoản', 'Users', 'Edit', 82),
('Users.Lock', 'Khóa tài khoản', 'Users', 'Lock', 83),
('Users.Unlock', 'Mở khóa tài khoản', 'Users', 'Unlock', 84),
('Users.ResetPassword', 'Đặt lại mật khẩu', 'Users', 'ResetPassword', 85),
('Users.Delete', 'Xóa tài khoản', 'Users', 'Delete', 86),
('Users.AssignPermission', 'Phân quyền', 'Users', 'AssignPermission', 87),
-- Settings
('Settings.View', 'Xem Cài đặt', 'Settings', 'View', 90),
('Settings.Edit', 'Chỉnh sửa Cài đặt', 'Settings', 'Edit', 91);

-- Gán toàn quyền cho QUAN_TRI
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
CROSS JOIN quyen q
WHERE vt.ma_vai_tro = 'QUAN_TRI';

-- Gán quyền cơ bản cho QUAN_LY (gợi ý)
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Dashboard.View',
  'Products.View','Products.Create','Products.Edit','Products.Delete','Products.Import','Products.Export',
  'Purchases.View','Purchases.Create','Purchases.Edit','Purchases.Import','Purchases.Export',
  'Invoices.View','Invoices.Create','Invoices.Edit','Invoices.Export','Invoices.Cancel',
  'Customers.View','Customers.Create','Customers.Edit','Customers.Delete','Customers.Export',
  'Suppliers.View','Suppliers.Create','Suppliers.Edit','Suppliers.Delete','Suppliers.Export',
  'CashBook.View','CashBook.CreateReceipt','CashBook.CreatePayment','CashBook.Edit','CashBook.Cancel','CashBook.Import','CashBook.Export',
  'Reports.View','Reports.Export',
  'Sales.View','Sales.Create',
  'Settings.View','Settings.Edit'
)
WHERE vt.ma_vai_tro = 'QUAN_LY';

-- THU_NGAN: bán hàng + khách hàng + sổ quỹ cơ bản
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Dashboard.View',
  'Sales.View','Sales.Create',
  'Invoices.View','Invoices.Create','Invoices.Edit',
  'Customers.View','Customers.Create','Customers.Edit',
  'CashBook.View','CashBook.CreateReceipt','CashBook.CreatePayment',
  'Products.View'
)
WHERE vt.ma_vai_tro = 'THU_NGAN';

-- KHO
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Dashboard.View',
  'Products.View','Products.Create','Products.Edit','Products.Import','Products.Export',
  'Purchases.View','Purchases.Create','Purchases.Edit','Purchases.Import','Purchases.Export',
  'Suppliers.View','Suppliers.Create','Suppliers.Edit',
  'Reports.View'
)
WHERE vt.ma_vai_tro = 'KHO';

-- BAO_CAO
INSERT IGNORE INTO vai_tro_quyen (vai_tro_id, quyen_id)
SELECT vt.id, q.id
FROM vai_tro vt
JOIN quyen q ON q.ma_quyen IN (
  'Reports.View','Reports.Export'
)
WHERE vt.ma_vai_tro = 'BAO_CAO';
