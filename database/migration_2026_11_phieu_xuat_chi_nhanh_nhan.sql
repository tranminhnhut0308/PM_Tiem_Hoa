-- Thêm cột chi nhánh nhận cho phiếu xuất chuyển kho
SET @db_name = DATABASE();

SET @ddl = (
  SELECT IF(
    COUNT(*) = 0,
    'ALTER TABLE `phieu_xuat_hang` ADD COLUMN `chi_nhanh_nhan_id` INT UNSIGNED NULL AFTER `chi_nhanh_id`;',
    'SELECT 1;'
  )
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db_name
    AND TABLE_NAME = 'phieu_xuat_hang'
    AND COLUMN_NAME = 'chi_nhanh_nhan_id'
);

PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
