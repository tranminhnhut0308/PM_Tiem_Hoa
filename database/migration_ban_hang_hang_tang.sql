-- ============================================================================
-- MIGRATION: BÁN HÀNG - HÀNG TẶNG KÈM & PHƯƠNG THỨC THANH TOÁN
-- ============================================================================
--
-- Mục đích
--   1) Ghi nhận hàng tặng kèm trên từng dòng chi tiết hóa đơn bán hàng
--      (hóa đơn in ra có nhãn "TẶNG KÈM" + tên chương trình khuyến mãi).
--   2) Lưu cấu hình khuyến mãi tặng kèm: hàng tặng + số lượng tặng theo
--      từng sản phẩm áp dụng, và loại khuyến mãi của chương trình.
--   3) Liên kết phiếu thu (phieu_chi) với hóa đơn bán hàng để biết
--      phương thức thanh toán khi mở lại hóa đơn.
--
-- Tính an toàn
--   * KHÔNG có DROP TABLE / DROP COLUMN / DELETE => không mất dữ liệu.
--   * Chạy lại nhiều lần vẫn an toàn (kiểm tra information_schema trước khi
--     thêm cột / tạo index).
--   * Nếu bảng chưa tồn tại thì BỎ QUA phần đó (không báo lỗi), để script
--     vẫn chạy được trên mọi database.
--   * MySQL không hỗ trợ "ADD COLUMN IF NOT EXISTS" nên dùng mẫu
--     information_schema + PREPARE (chạy được trên cả MySQL lẫn MariaDB).
--
-- Lưu ý
--   Ứng dụng TỰ kiểm tra và tự thêm các cột này khi khởi động
--   (xem Services/DatabaseService.cs: EnsureChiTietHoaDonHangTangColumnsAsync,
--   EnsurePhieuQuyHoaDonColumnsAsync, EnsureBangKhuyenMaiAsync /
--   EnsureCotBangKhuyenMaiAsync). Script này làm đúng những việc đó, chạy
--   trước khi mở bán hàng lần đầu để không phải chờ ứng dụng tự sửa.
--
-- Áp dụng
--   mysql -u <user> -p <database> < migration_ban_hang_hang_tang.sql
--

-- ---------------------------------------------------------------------------
-- 1. CHI TIẾT HÓA ĐƠN BÁN HÀNG: thêm cột ghi nhận hàng tặng kèm
-- ---------------------------------------------------------------------------
-- is_hang_tang     : 1 = dòng này là hàng tặng kèm (giá 0, không tính doanh thu)
-- khuyen_mai_id    : id chương trình khuyến mãi đã áp dụng
-- khuyen_mai_ten   : tên chương trình (lưu kèm để in lại hóa đơn cũ)
-- san_pham_id_mua  : id sản phẩm được mua (sản phẩm sinh ra hàng tặng)
-- so_luong_mua     : số lượng sản phẩm đã mua (để đối chiếu điều kiện tặng)

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang') = 0,
  'SELECT ''chi_tiet_hoa_don_ban_hang: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND COLUMN_NAME = 'is_hang_tang') > 0,
    'SELECT ''chi_tiet_hoa_don_ban_hang.is_hang_tang: đã có'' AS info',
    'ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN is_hang_tang tinyint(1) NOT NULL DEFAULT 0 AFTER thanh_tien'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang') = 0,
  'SELECT ''chi_tiet_hoa_don_ban_hang: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND COLUMN_NAME = 'khuyen_mai_id') > 0,
    'SELECT ''chi_tiet_hoa_don_ban_hang.khuyen_mai_id: đã có'' AS info',
    'ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN khuyen_mai_id int(10) unsigned DEFAULT NULL AFTER is_hang_tang'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang') = 0,
  'SELECT ''chi_tiet_hoa_don_ban_hang: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND COLUMN_NAME = 'khuyen_mai_ten') > 0,
    'SELECT ''chi_tiet_hoa_don_ban_hang.khuyen_mai_ten: đã có'' AS info',
    'ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN khuyen_mai_ten varchar(150) DEFAULT NULL AFTER khuyen_mai_id'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang') = 0,
  'SELECT ''chi_tiet_hoa_don_ban_hang: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND COLUMN_NAME = 'san_pham_id_mua') > 0,
    'SELECT ''chi_tiet_hoa_don_ban_hang.san_pham_id_mua: đã có'' AS info',
    'ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN san_pham_id_mua int(10) unsigned DEFAULT NULL AFTER khuyen_mai_ten'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang') = 0,
  'SELECT ''chi_tiet_hoa_don_ban_hang: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND COLUMN_NAME = 'so_luong_mua') > 0,
    'SELECT ''chi_tiet_hoa_don_ban_hang.so_luong_mua: đã có'' AS info',
    'ALTER TABLE chi_tiet_hoa_don_ban_hang ADD COLUMN so_luong_mua decimal(15,3) DEFAULT NULL AFTER san_pham_id_mua'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Index tra cứu nhanh: hóa đơn nào có hàng tặng
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang') = 0,
  'SELECT ''chi_tiet_hoa_don_ban_hang: bảng chưa tồn tại, bỏ qua index'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND INDEX_NAME = 'idx_cthdbh_hang_tang') > 0,
    'SELECT ''chi_tiet_hoa_don_ban_hang.idx_cthdbh_hang_tang: đã có'' AS info',
    'CREATE INDEX idx_cthdbh_hang_tang ON chi_tiet_hoa_don_ban_hang (hoa_don_id, is_hang_tang)'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 2. PHIẾU THU (phieu_chi): liên kết với hóa đơn bán hàng
-- ---------------------------------------------------------------------------
-- Khi mở lại hóa đơn, ứng dụng đọc phuong_thuc của phiếu thu gắn với hóa đơn
-- để hiển thị đúng phương thức thanh toán (tiền mặt / chuyển khoản / thẻ...).

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi') = 0,
  'SELECT ''phieu_chi: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi'
         AND COLUMN_NAME = 'hoa_don_ban_hang_id') > 0,
    'SELECT ''phieu_chi.hoa_don_ban_hang_id: đã có'' AS info',
    'ALTER TABLE phieu_chi ADD COLUMN hoa_don_ban_hang_id int(10) unsigned DEFAULT NULL AFTER trang_thai'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi') = 0,
  'SELECT ''phieu_chi: bảng chưa tồn tại, bỏ qua'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi'
         AND COLUMN_NAME = 'ma_hoa_don') > 0,
    'SELECT ''phieu_chi.ma_hoa_don: đã có'' AS info',
    'ALTER TABLE phieu_chi ADD COLUMN ma_hoa_don varchar(50) DEFAULT NULL AFTER hoa_don_ban_hang_id'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Mỗi hóa đơn chỉ có 1 phiếu thu (cột cho phép NULL nên phiếu thu tay vẫn
-- insert bình thường).
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.TABLES
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi') = 0,
  'SELECT ''phieu_chi: bảng chưa tồn tại, bỏ qua index'' AS info',
  IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'phieu_chi'
         AND INDEX_NAME = 'uk_phieu_chi_hoa_don') > 0,
    'SELECT ''phieu_chi.uk_phieu_chi_hoa_don: đã có'' AS info',
    'CREATE UNIQUE INDEX uk_phieu_chi_hoa_don ON phieu_chi (hoa_don_ban_hang_id)'));
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 3. KHUYẾN MÃI: bảng chương trình + chi tiết (có hàng tặng kèm)
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `chuong_trinh_khuyen_mai` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_chuong_trinh` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_chuong_trinh` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ngay_bat_dau` datetime NOT NULL,
  `ngay_ket_thuc` datetime NOT NULL,
  `nhom_khach_hang_id` int(10) unsigned DEFAULT NULL,
  `trang_thai` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DANG_HOAT_DONG',
  `loai_khuyen_mai` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'THEO_HANG_HOA',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `chi_nhanh_id` int(10) unsigned NOT NULL DEFAULT 1,
  `nguoi_tao_id` int(10) unsigned DEFAULT NULL,
  `ngay_tao` datetime DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_ctkm_ma` (`ma_chuong_trinh`),
  KEY `idx_ctkm_ngay` (`ngay_bat_dau`,`ngay_ket_thuc`),
  KEY `idx_ctkm_trangthai` (`trang_thai`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `chi_tiet_khuyen_mai` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `chuong_trinh_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `don_vi_tinh` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_luong_mua` decimal(15,2) NOT NULL DEFAULT 1.00,
  `loai_giam_gia` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PHAN_TRAM',
  `gia_tri_giam` decimal(15,2) NOT NULL DEFAULT 0.00,
  `so_luong_tang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `san_pham_tang_id` int(10) unsigned DEFAULT NULL,
  `ten_san_pham_tang` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_ctkm_ct` (`chuong_trinh_id`),
  KEY `idx_ctkm_sp` (`san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3.1 Chuong trinh khuyen mai: các cot moi (thứ tự ADD COLUMN phải khớp
--     với thứ tự cột trong bảng để "AFTER ..." không bị lỗi)
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'ngay_bat_dau') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.ngay_bat_dau: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_bat_dau DATETIME NULL AFTER ten_chuong_trinh');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'ngay_ket_thuc') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.ngay_ket_thuc: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_ket_thuc DATETIME NULL AFTER ngay_bat_dau');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Chuyển dữ liệu từ bảng KM kiểu cũ: thoi_gian_bat_dau / thoi_gian_ket_thuc
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'thoi_gian_bat_dau') > 0,
  'UPDATE chuong_trinh_khuyen_mai SET ngay_bat_dau = thoi_gian_bat_dau WHERE ngay_bat_dau IS NULL',
  'SELECT ''chuong_trinh_khuyen_mai: không có cột cũ thoi_gian_bat_dau'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'thoi_gian_ket_thuc') > 0,
  'UPDATE chuong_trinh_khuyen_mai SET ngay_ket_thuc = thoi_gian_ket_thuc WHERE ngay_ket_thuc IS NULL',
  'SELECT ''chuong_trinh_khuyen_mai: không có cột cũ thoi_gian_ket_thuc'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Hai cột ngày không được để trống
UPDATE chuong_trinh_khuyen_mai SET ngay_bat_dau = '2000-01-01 00:00:00' WHERE ngay_bat_dau IS NULL;
UPDATE chuong_trinh_khuyen_mai SET ngay_ket_thuc = '2099-12-31 23:59:59' WHERE ngay_ket_thuc IS NULL;
ALTER TABLE chuong_trinh_khuyen_mai MODIFY COLUMN ngay_bat_dau DATETIME NOT NULL;
ALTER TABLE chuong_trinh_khuyen_mai MODIFY COLUMN ngay_ket_thuc DATETIME NOT NULL;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'nhom_khach_hang_id') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.nhom_khach_hang_id: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN nhom_khach_hang_id INT UNSIGNED NULL AFTER ngay_ket_thuc');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'doi_tac_id') > 0,
  'UPDATE chuong_trinh_khuyen_mai SET nhom_khach_hang_id = doi_tac_id WHERE nhom_khach_hang_id IS NULL',
  'SELECT ''chuong_trinh_khuyen_mai: không có cột cũ doi_tac_id'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- trang_thai: bản cũ dùng tinyint(1), bản mới dùng chuỗi trạng thái
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'trang_thai') > 0,
  IF(
    (SELECT DATA_TYPE FROM information_schema.COLUMNS
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
         AND COLUMN_NAME = 'trang_thai') IN ('varchar','char','text'),
    'SELECT ''chuong_trinh_khuyen_mai.trang_thai: đã là kiểu chuỗi'' AS info',
    'UPDATE chuong_trinh_khuyen_mai SET trang_thai = 1'),
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN trang_thai VARCHAR(50) NOT NULL DEFAULT ''DANG_HOAT_DONG'' AFTER nhom_khach_hang_id');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT DATA_TYPE FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'trang_thai') IN ('varchar','char','text'),
  'SELECT ''chuong_trinh_khuyen_mai.trang_thai: đã là kiểu chuỗi'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai MODIFY COLUMN trang_thai VARCHAR(50) NOT NULL DEFAULT ''DANG_HOAT_DONG''');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

UPDATE chuong_trinh_khuyen_mai SET trang_thai = 'DANG_HOAT_DONG' WHERE trang_thai = '1';

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'loai_khuyen_mai') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.loai_khuyen_mai: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN loai_khuyen_mai VARCHAR(50) NOT NULL DEFAULT ''THEO_HANG_HOA'' AFTER trang_thai');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'ghi_chu') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.ghi_chu: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ghi_chu TEXT NULL AFTER loai_khuyen_mai');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'mo_ta') > 0,
  'UPDATE chuong_trinh_khuyen_mai SET ghi_chu = mo_ta WHERE (ghi_chu IS NULL OR ghi_chu = '''') AND mo_ta IS NOT NULL',
  'SELECT ''chuong_trinh_khuyen_mai: không có cột cũ mo_ta'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'chi_nhanh_id') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.chi_nhanh_id: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1 AFTER ghi_chu');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'nguoi_tao_id') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.nguoi_tao_id: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN nguoi_tao_id INT UNSIGNED NULL AFTER chi_nhanh_id');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'ngay_tao') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.ngay_tao: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_tao DATETIME DEFAULT CURRENT_TIMESTAMP AFTER nguoi_tao_id');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'ngay_cap_nhat') > 0,
  'SELECT ''chuong_trinh_khuyen_mai.ngay_cap_nhat: đã có'' AS info',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER ngay_tao');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 3.2 Chi tiet khuyen mai: cot san pham + gia + hàng tặng kèm
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'san_pham_id') > 0,
  'SELECT ''chi_tiet_khuyen_mai.san_pham_id: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN san_pham_id INT UNSIGNED NOT NULL DEFAULT 0 AFTER chuong_trinh_id');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'hang_hoa_id') > 0,
  'UPDATE chi_tiet_khuyen_mai SET san_pham_id = hang_hoa_id WHERE san_pham_id = 0',
  'SELECT ''chi_tiet_khuyen_mai: không có cột cũ hang_hoa_id'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'ma_san_pham') > 0,
  'SELECT ''chi_tiet_khuyen_mai.ma_san_pham: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ma_san_pham VARCHAR(50) NULL AFTER san_pham_id');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'ten_san_pham') > 0,
  'SELECT ''chi_tiet_khuyen_mai.ten_san_pham: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ten_san_pham VARCHAR(255) NULL AFTER ma_san_pham');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'don_vi_tinh') > 0,
  'SELECT ''chi_tiet_khuyen_mai.don_vi_tinh: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN don_vi_tinh VARCHAR(50) NULL AFTER ten_san_pham');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_luong_mua') > 0,
  'SELECT ''chi_tiet_khuyen_mai.so_luong_mua: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN so_luong_mua DECIMAL(15,2) NOT NULL DEFAULT 1 AFTER don_vi_tinh');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'loai_giam_gia') > 0,
  'SELECT ''chi_tiet_khuyen_mai.loai_giam_gia: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN loai_giam_gia VARCHAR(20) NOT NULL DEFAULT ''PHAN_TRAM'' AFTER so_luong_mua');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'gia_tri_giam') > 0,
  'SELECT ''chi_tiet_khuyen_mai.gia_tri_giam: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_giam_gia');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'phan_tram_giam_gia') > 0,
  'UPDATE chi_tiet_khuyen_mai SET gia_tri_giam = phan_tram_giam_gia WHERE gia_tri_giam = 0 AND phan_tram_giam_gia > 0',
  'SELECT ''chi_tiet_khuyen_mai: không có cột cũ phan_tram_giam_gia'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_tien_giam') > 0,
  'UPDATE chi_tiet_khuyen_mai SET gia_tri_giam = so_tien_giam WHERE gia_tri_giam = 0 AND so_tien_giam > 0',
  'SELECT ''chi_tiet_khuyen_mai: không có cột cũ so_tien_giam'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_tien_giam') > 0,
  'UPDATE chi_tiet_khuyen_mai SET loai_giam_gia = ''SO_TIEN'' WHERE loai_giam_gia = ''PHAN_TRAM'' AND so_tien_giam > 0 AND phan_tram_giam_gia = 0',
  'SELECT ''chi_tiet_khuyen_mai: không có cột cũ so_tien_giam'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_luong_tang') > 0,
  'SELECT ''chi_tiet_khuyen_mai.so_luong_tang: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN so_luong_tang DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER gia_tri_giam');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_luong_hang_tang') > 0,
  'UPDATE chi_tiet_khuyen_mai SET so_luong_tang = so_luong_hang_tang WHERE so_luong_tang = 0 AND so_luong_hang_tang > 0',
  'SELECT ''chi_tiet_khuyen_mai: không có cột cũ so_luong_hang_tang'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'san_pham_tang_id') > 0,
  'SELECT ''chi_tiet_khuyen_mai.san_pham_tang_id: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN san_pham_tang_id INT UNSIGNED NULL AFTER so_luong_tang');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'hang_hoa_tang_id') > 0,
  'UPDATE chi_tiet_khuyen_mai SET san_pham_tang_id = hang_hoa_tang_id WHERE san_pham_tang_id IS NULL',
  'SELECT ''chi_tiet_khuyen_mai: không có cột cũ hang_hoa_tang_id'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'ten_san_pham_tang') > 0,
  'SELECT ''chi_tiet_khuyen_mai.ten_san_pham_tang: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ten_san_pham_tang VARCHAR(255) NULL AFTER san_pham_tang_id');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'ghi_chu') > 0,
  'SELECT ''chi_tiet_khuyen_mai.ghi_chu: đã có'' AS info',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ghi_chu VARCHAR(255) NULL AFTER ten_san_pham_tang');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 3.3 Bảng cũ khai báo hang_hoa_id / don_vi_tinh_id là NOT NULL (kèm khoá ngoại
--     sang bảng hàng hóa cũ). Ứng dụng mới chỉ ghi bộ cột "san_pham_*" nên hai
--     cột cũ phải cho phép NULL, nếu không lưu khuyến mãi sẽ lỗi
--     "doesn't have a default value" hoặc vi phạm khoá ngoại.
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'hang_hoa_id' AND IS_NULLABLE = 'NO') > 0,
  'ALTER TABLE chi_tiet_khuyen_mai MODIFY COLUMN hang_hoa_id INT(10) UNSIGNED NULL DEFAULT NULL',
  'SELECT ''chi_tiet_khuyen_mai.hang_hoa_id: không cần nới (không có hoặc đã cho phép NULL)'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'don_vi_tinh_id' AND IS_NULLABLE = 'NO') > 0,
  'ALTER TABLE chi_tiet_khuyen_mai MODIFY COLUMN don_vi_tinh_id INT(10) UNSIGNED NULL DEFAULT NULL',
  'SELECT ''chi_tiet_khuyen_mai.don_vi_tinh_id: không cần nới (không có hoặc đã cho phép NULL)'' AS info');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------------
-- 4. BẢNG LỊCH SỬ NHẬP XUẤT KHO (bắt buộc cho luồng bán hàng)
-- ---------------------------------------------------------------------------
-- Mỗi lần xuất hàng (bán lẻ, phiếu xuất kho, hủy hóa đơn) đều ghi 1 dòng
-- vết xuất vào lich_su_nhap_xuat_kho. Database ERP cũ chưa có bảng này thì
-- lệnh INSERT sẽ lỗi "Table doesn't exist" và hủy cả giao dịch tạo hóa đơn.
-- Ứng dụng cũng tự tạo bảng này lúc chạy
-- (xem Services/DatabaseService.cs: EnsureBangLichSuNhapXuatKhoAsync).
CREATE TABLE IF NOT EXISTS `lich_su_nhap_xuat_kho` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `loai_giao_dich` varchar(30) NOT NULL,
  `loai_chung_tu` varchar(30) DEFAULT NULL,
  `ma_chung_tu` varchar(50) DEFAULT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `ton_truoc` decimal(15,3) NOT NULL DEFAULT 0.000,
  `ton_sau` decimal(15,3) NOT NULL DEFAULT 0.000,
  `gia_von` decimal(15,2) NOT NULL DEFAULT 0.00,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  KEY `ix_lsnxk_sp` (`san_pham_id`),
  KEY `ix_lsnxk_thoi_gian` (`thoi_gian`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- 5. KIỂM TRA KẾT QUẢ
-- ---------------------------------------------------------------------------
SELECT 'Đã hoàn tất migration bán hàng hàng tặng kèm' AS ket_qua;

SELECT TABLE_NAME AS bang, COLUMN_NAME AS cot, COLUMN_TYPE AS kieu
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (
        (TABLE_NAME = 'chi_tiet_hoa_don_ban_hang'
         AND COLUMN_NAME IN ('is_hang_tang','khuyen_mai_id','khuyen_mai_ten','san_pham_id_mua','so_luong_mua'))
     OR (TABLE_NAME = 'chi_tiet_khuyen_mai'
         AND COLUMN_NAME IN ('san_pham_id','ma_san_pham','ten_san_pham','don_vi_tinh','so_luong_mua','loai_giam_gia','gia_tri_giam','so_luong_tang','san_pham_tang_id','ten_san_pham_tang','ghi_chu'))
     OR (TABLE_NAME = 'chuong_trinh_khuyen_mai'
         AND COLUMN_NAME IN ('ngay_bat_dau','ngay_ket_thuc','nhom_khach_hang_id','trang_thai','loai_khuyen_mai','ghi_chu','chi_nhanh_id','nguoi_tao_id','ngay_tao','ngay_cap_nhat'))
     OR (TABLE_NAME = 'phieu_chi'
         AND COLUMN_NAME IN ('hoa_don_ban_hang_id','ma_hoa_don'))
  )
ORDER BY TABLE_NAME, COLUMN_NAME;
