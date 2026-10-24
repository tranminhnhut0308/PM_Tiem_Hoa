-- ============================================================================
-- MIGRATION: Thiết kế lại quản lý chương trình khuyến mãi (dùng chung)
-- - Bỏ phân loại nhóm khách hàng
-- - Chuyển sang 3 loại: ORDER_DISCOUNT, PRODUCT_DISCOUNT, BUY_X_GET_Y
-- - Hỗ trợ điều kiện/ưu đãi theo tổng đơn hoặc theo sản phẩm/số lượng + tặng quà
-- ============================================================================
-- An toàn: chỉ ADD COLUMN/UPDATE, không DROP dữ liệu.

SET NAMES utf8mb4;

-- 1) Cập nhật bảng chuong_trinh_khuyen_mai
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'loai_khuyen_mai') > 0,
  'SELECT 1',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN loai_khuyen_mai VARCHAR(50) NOT NULL DEFAULT ''PRODUCT_DISCOUNT'' AFTER trang_thai'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

UPDATE chuong_trinh_khuyen_mai SET loai_khuyen_mai = 'ORDER_DISCOUNT' WHERE loai_khuyen_mai IN ('THEO_HOA_DON');
UPDATE chuong_trinh_khuyen_mai SET loai_khuyen_mai = 'BUY_X_GET_Y' WHERE loai_khuyen_mai IN ('TANG_KEM');
UPDATE chuong_trinh_khuyen_mai SET loai_khuyen_mai = 'PRODUCT_DISCOUNT' WHERE loai_khuyen_mai IN ('THEO_HANG_HOA') OR loai_khuyen_mai IS NULL;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'gia_tri_don_toi_thieu') > 0,
  'SELECT 1',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN gia_tri_don_toi_thieu DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_khuyen_mai'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'loai_giam_gia') > 0,
  'SELECT 1',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN loai_giam_gia VARCHAR(20) NOT NULL DEFAULT ''PHAN_TRAM'' AFTER gia_tri_don_toi_thieu'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
       AND COLUMN_NAME = 'gia_tri_giam') > 0,
  'SELECT 1',
  'ALTER TABLE chuong_trinh_khuyen_mai ADD COLUMN gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_giam_gia'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Chương trình dùng chung: không còn ràng buộc nhóm khách hàng
UPDATE chuong_trinh_khuyen_mai SET nhom_khach_hang_id = NULL WHERE nhom_khach_hang_id IS NOT NULL;

-- 2) Cập nhật bảng chi_tiet_khuyen_mai
SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_luong_mua') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN so_luong_mua DECIMAL(15,2) NOT NULL DEFAULT 1 AFTER don_vi_tinh'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'loai_giam_gia') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN loai_giam_gia VARCHAR(20) NOT NULL DEFAULT ''PHAN_TRAM'' AFTER so_luong_mua'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'gia_tri_giam') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN gia_tri_giam DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER loai_giam_gia'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'so_luong_tang') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN so_luong_tang DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER gia_tri_giam'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'san_pham_tang_id') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN san_pham_tang_id INT UNSIGNED NULL AFTER so_luong_tang'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl = IF(
  (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chi_tiet_khuyen_mai'
       AND COLUMN_NAME = 'ten_san_pham_tang') > 0,
  'SELECT 1',
  'ALTER TABLE chi_tiet_khuyen_mai ADD COLUMN ten_san_pham_tang VARCHAR(255) NULL AFTER san_pham_tang_id'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SELECT 'Migration khuyến mãi dùng chung hoàn tất' AS result;
