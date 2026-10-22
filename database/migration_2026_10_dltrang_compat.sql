-- =====================================================================
-- MIGRATION LỚP TƯƠNG THÍCH ỨNG DỤNG
-- Database : shophoa_Huy_tiemhoatest_DLTrang
-- Ngày     : 2026-10-22
-- ---------------------------------------------------------------------
-- Mục đích:
--   1) Tạo các bảng lớp tương thích (section 7.6 của
--      database/shophoa_Huy_tiemhoatest_fixed.sql) mà ứng dụng .NET
--      cần nhưng database ERP chưa có.
--   2) Bổ sung cột cho các bảng ERP đã tồn tại nhưng shape cũ.
--   3) Seed dữ liệu nền từ chính dữ liệu ERP hiện có.
--
-- Nguyên tắc bắt buộc:
--   * Idempotent: chạy lại bao nhiêu lần cũng an toàn.
--   * KHÔNG DROP / TRUNCATE / DELETE: giữ nguyên toàn bộ dữ liệu ERP.
--   * MariaDB 10.1 không có "ALTER TABLE ... ADD COLUMN IF NOT EXISTS"
--     nên phần bổ sung cột dùng information_schema + PREPARE/EXECUTE
--     để chỉ thêm đúng những cột còn thiếu.
--   * Không dùng stored procedure / DELIMITER để file chạy được trên
--     cả mysql CLI, phpMyAdmin và MySqlConnector (multi-statement).
--
-- Lưu ý: DDL trong MySQL/MariaDB tự commit, không thể rollback.
-- =====================================================================

SET NAMES utf8mb4;
SET SESSION group_concat_max_len = 1048576;

USE `shophoa_Huy_tiemhoatest_DLTrang`;


-- =====================================================================
-- PHẦN 1 : BỔ SUNG CỘT CHO CÁC BẢNG ERP ĐÃ TỒN TẠI
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1.1 nguoi_dung : hồ sơ nhân viên (mã NV, SĐT, email, ngày vào làm),
--     chi nhánh và thời điểm đăng nhập cuối.
--     (Cột có sẵn trong DB sẽ được giữ nguyên.)
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ma_nguoi_dung' AS c_name, 'VARCHAR(50) NULL' AS c_def UNION ALL
        SELECT 2, 'so_dien_thoai', 'VARCHAR(30) NULL' UNION ALL
        SELECT 3, 'email', 'VARCHAR(150) NULL' UNION ALL
        SELECT 4, 'ngay_vao_lam', 'DATE NULL' UNION ALL
        SELECT 5, 'chi_nhanh_id', 'INT(10) UNSIGNED NOT NULL DEFAULT 1' UNION ALL
        SELECT 6, 'lan_dang_nhap_cuoi', 'DATETIME NULL' UNION ALL
        SELECT 7, 'ngay_cap_nhat', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'nguoi_dung'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `nguoi_dung` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.2 so_quy_dau_ky : bảng ERP cũ chỉ có ky_ke_toan_id / tien_mat /
--     tien_trong_tai_khoan, còn ứng dụng ghi theo ngày + phương thức.
--     ky_ke_toan_id phải cho phép NULL vì ứng dụng không truyền cột này
--     (giữ nguyên giá trị của các bản ghi hiện có).
-- ---------------------------------------------------------------------
ALTER TABLE `so_quy_dau_ky` MODIFY COLUMN `ky_ke_toan_id` INT(10) UNSIGNED NULL DEFAULT NULL;

SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ngay_dau_ky' AS c_name, 'DATE NULL' AS c_def UNION ALL
        SELECT 2, 'phuong_thuc', 'VARCHAR(50) NOT NULL DEFAULT ''TIEN_MAT''' UNION ALL
        SELECT 3, 'so_tien', 'DECIMAL(15,2) NOT NULL DEFAULT 0.00' UNION ALL
        SELECT 4, 'noi_dung', 'VARCHAR(255) NULL' UNION ALL
        SELECT 5, 'chi_nhanh_id', 'INT(10) UNSIGNED NOT NULL DEFAULT 1' UNION ALL
        SELECT 6, 'nguoi_tao_id', 'INT(10) UNSIGNED NULL' UNION ALL
        SELECT 7, 'ngay_tao', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP' UNION ALL
        SELECT 8, 'ngay_cap_nhat', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'so_quy_dau_ky'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `so_quy_dau_ky` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.3 chi_tiet_cong_no_dau_ky : ứng dụng ghi kỳ nợ theo loại đối tác
--     (KHACH_HANG / NHA_CUNG_CAP) + ngày đầu kỳ.
--     ky_ke_toan_id cho phép NULL vì ứng dụng không truyền cột này.
-- ---------------------------------------------------------------------
ALTER TABLE `chi_tiet_cong_no_dau_ky` MODIFY COLUMN `ky_ke_toan_id` INT(10) UNSIGNED NULL DEFAULT NULL;

SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'loai_doi_tac' AS c_name, 'VARCHAR(20) NOT NULL DEFAULT ''''' AS c_def UNION ALL
        SELECT 2, 'ma_doi_tac', 'VARCHAR(50) NULL' UNION ALL
        SELECT 3, 'ten_doi_tac', 'VARCHAR(255) NULL' UNION ALL
        SELECT 4, 'so_dien_thoai', 'VARCHAR(50) NULL' UNION ALL
        SELECT 5, 'ngay_dau_ky', 'DATE NULL' UNION ALL
        SELECT 6, 'ngay_hen_tra', 'DATE NULL' UNION ALL
        SELECT 7, 'chi_nhanh_id', 'INT(10) UNSIGNED NOT NULL DEFAULT 1' UNION ALL
        SELECT 8, 'nguoi_tao_id', 'INT(10) UNSIGNED NULL' UNION ALL
        SELECT 9, 'ngay_tao', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP' UNION ALL
        SELECT 10, 'ngay_cap_nhat', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'chi_tiet_cong_no_dau_ky'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `chi_tiet_cong_no_dau_ky` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.4 chi_tiet_hang_hoa_dau_ky : thêm cột thời gian và khóa duy nhất
--     (kho_hang_id, hang_hoa_id, ky_ke_toan_id) để câu
--     "ON DUPLICATE KEY UPDATE" của ứng dụng hoạt động đúng.
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ngay_tao' AS c_name, 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP' AS c_def UNION ALL
        SELECT 2, 'ngay_cap_nhat', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'chi_tiet_hang_hoa_dau_ky'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `chi_tiet_hang_hoa_dau_ky` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE `chi_tiet_hang_hoa_dau_ky` ADD UNIQUE KEY `uk_kho_hang_ky` (`kho_hang_id`, `hang_hoa_id`, `ky_ke_toan_id`)',
        'SELECT 1')
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'chi_tiet_hang_hoa_dau_ky'
      AND INDEX_NAME = 'uk_kho_hang_ky'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.5 ky_ke_toan : thêm mốc thời gian cho khớp với định nghĩa trong code.
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ngay_tao' AS c_name, 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP' AS c_def UNION ALL
        SELECT 2, 'ngay_cap_nhat', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'ky_ke_toan'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `ky_ke_toan` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.6 Hai view ERP (v_ton_kho_hien_tai, v_cong_no_doi_tac) trong database
--     _DLTrang đang trỏ sang database shophoa_Huy_tiemhoatest (database cũ).
--     Sửa lại cho chúng đọc đúng dữ liệu của database hiện tại, nếu không
--     tồn kho / công nợ lấy từ đây sẽ không khớp với dữ liệu ERP đang dùng.
-- ---------------------------------------------------------------------
SET @vd = (
    SELECT REPLACE(
             VIEW_DEFINITION,
             '`shophoa_Huy_tiemhoatest`.',
             CONCAT('`', DATABASE(), '`.'))
    FROM information_schema.VIEWS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'v_ton_kho_hien_tai'
);
SET @vd = IF(@vd IS NULL, 'SELECT 1', CONCAT('CREATE OR REPLACE VIEW `v_ton_kho_hien_tai` AS ', @vd));
PREPARE stmt FROM @vd;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @vd = (
    SELECT REPLACE(
             VIEW_DEFINITION,
             '`shophoa_Huy_tiemhoatest`.',
             CONCAT('`', DATABASE(), '`.'))
    FROM information_schema.VIEWS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'v_cong_no_doi_tac'
);
SET @vd = IF(@vd IS NULL, 'SELECT 1', CONCAT('CREATE OR REPLACE VIEW `v_cong_no_doi_tac` AS ', @vd));
PREPARE stmt FROM @vd;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.7 vai_tro_quyen : bảng ERP dùng cột `vai_tro` (VARCHAR) và có
--     FOREIGN KEY sang bảng RỖNG `quyen_he_thong`, trong khi ứng dụng
--     dùng `vai_tro_id` (INT) và tham chiếu bảng `quyen` (101 quyền).
--     => Bỏ FK cũ (bảng đích rỗng nên mọi INSERT của ứng dụng sẽ lỗi),
--        cho phép NULL cột `vai_tro`, thêm `vai_tro_id`,
--        `nguoi_cap_nhat_id`, `ngay_cap_nhat` và khóa UNIQUE
--        (vai_tro_id, quyen_id) đúng như ứng dụng INSERT IGNORE.
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT IF(COUNT(*) > 0,
              'ALTER TABLE `vai_tro_quyen` DROP FOREIGN KEY `fk_vtq_quyen`',
              'SELECT 1')
    FROM information_schema.REFERENTIAL_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'vai_tro_quyen'
      AND CONSTRAINT_NAME = 'fk_vtq_quyen'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT IF(`IS_NULLABLE` = 'NO',
              'ALTER TABLE `vai_tro_quyen` MODIFY COLUMN `vai_tro` VARCHAR(30) NULL DEFAULT NULL',
              'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'vai_tro_quyen'
      AND COLUMN_NAME = 'vai_tro'
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', @ddl);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'vai_tro_id' AS c_name, 'INT(10) UNSIGNED NULL' AS c_def UNION ALL
        SELECT 2, 'nguoi_cap_nhat_id', 'INT(10) UNSIGNED NULL' UNION ALL
        SELECT 3, 'ngay_cap_nhat', 'DATETIME NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'vai_tro_quyen'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `vai_tro_quyen` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- Ánh xạ vai trò (theo tên) sang vai_tro_id (theo số) cho bản ghi sẵn có
UPDATE `vai_tro_quyen` vtq
  JOIN `vai_tro` vt ON vt.ma_vai_tro = vtq.vai_tro
  SET vtq.vai_tro_id = vt.id
 WHERE vtq.vai_tro_id IS NULL;

SET @ddl = (
    SELECT IF(
        (SELECT COUNT(*) FROM (
            SELECT vai_tro_id, quyen_id
              FROM `vai_tro_quyen`
             WHERE vai_tro_id IS NOT NULL
             GROUP BY vai_tro_id, quyen_id
            HAVING COUNT(*) > 1
        ) dups) = 0
        AND (SELECT COUNT(*) FROM information_schema.STATISTICS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = 'vai_tro_quyen'
                AND INDEX_NAME = 'uk_vai_tro_quyen_id') = 0,
        'ALTER TABLE `vai_tro_quyen` ADD UNIQUE KEY `uk_vai_tro_quyen_id` (`vai_tro_id`, `quyen_id`)',
        'SELECT 1')
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.8 chi_tiet_phieu_tra_hang : thêm `chi_tiet_hoa_don_id` (liên kết
--     dòng hàng hóa của hóa đơn) và `ghi_chu` theo shape app.
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'chi_tiet_hoa_don_id' AS c_name, 'INT(10) UNSIGNED NULL' AS c_def UNION ALL
        SELECT 2, 'ghi_chu', 'TEXT NULL'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'chi_tiet_phieu_tra_hang'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `chi_tiet_phieu_tra_hang` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- ---------------------------------------------------------------------
-- 1.9 chuong_trinh_khuyen_mai : bảng ERP dùng thoi_gian_bat_dau /
--     thoi_gian_ket_thuc / doi_tac_id / trang_thai (tinyint) / mo_ta.
--     Ứng dụng dùng ngay_bat_dau / ngay_ket_thuc / nhom_khach_hang_id /
--     trang_thai (VARCHAR) / loai_khuyen_mai / ghi_chu / chi_nhanh_id /
--     nguoi_tao_id / ngay_tao / ngay_cap_nhat.
--     => Thêm cột mới, chuyển `trang_thai` sang VARCHAR và chuyển dữ liệu.
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ngay_bat_dau' AS c_name, 'DATETIME NULL' AS c_def UNION ALL
        SELECT 2, 'ngay_ket_thuc', 'DATETIME NULL' UNION ALL
        SELECT 3, 'nhom_khach_hang_id', 'INT(10) UNSIGNED NULL' UNION ALL
        SELECT 4, 'loai_khuyen_mai', 'VARCHAR(50) NOT NULL DEFAULT ''THEO_HANG_HOA''' UNION ALL
        SELECT 5, 'ghi_chu', 'TEXT NULL' UNION ALL
        SELECT 6, 'chi_nhanh_id', 'INT(10) UNSIGNED NOT NULL DEFAULT 1' UNION ALL
        SELECT 7, 'nguoi_tao_id', 'INT(10) UNSIGNED NULL' UNION ALL
        SELECT 8, 'ngay_tao', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP' UNION ALL
        SELECT 9, 'ngay_cap_nhat', 'DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'chuong_trinh_khuyen_mai'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `chuong_trinh_khuyen_mai` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- `trang_thai` của ERP là tinyint(1) nên không nhận được giá trị
-- 'DANG_HOAT_DONG' của ứng dụng -> đổi sang VARCHAR và quy đổi giá trị.
SET @ddl = (
    SELECT IF(`DATA_TYPE` <> 'varchar',
              'ALTER TABLE `chuong_trinh_khuyen_mai` MODIFY COLUMN `trang_thai` VARCHAR(50) NOT NULL DEFAULT ''DANG_HOAT_DONG''',
              'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'chuong_trinh_khuyen_mai'
      AND COLUMN_NAME = 'trang_thai'
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', @ddl);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE `chuong_trinh_khuyen_mai`
   SET `ngay_bat_dau` = `thoi_gian_bat_dau`
 WHERE `ngay_bat_dau` IS NULL AND `thoi_gian_bat_dau` IS NOT NULL;

UPDATE `chuong_trinh_khuyen_mai`
   SET `ngay_ket_thuc` = `thoi_gian_ket_thuc`
 WHERE `ngay_ket_thuc` IS NULL AND `thoi_gian_ket_thuc` IS NOT NULL;

UPDATE `chuong_trinh_khuyen_mai`
   SET `ghi_chu` = `mo_ta`
 WHERE (`ghi_chu` IS NULL OR `ghi_chu` = '') AND `mo_ta` IS NOT NULL;

UPDATE `chuong_trinh_khuyen_mai`
   SET `nhom_khach_hang_id` = `doi_tac_id`
 WHERE `nhom_khach_hang_id` IS NULL AND `doi_tac_id` IS NOT NULL;

UPDATE `chuong_trinh_khuyen_mai` SET `ngay_bat_dau` = '2000-01-01 00:00:00' WHERE `ngay_bat_dau` IS NULL;
UPDATE `chuong_trinh_khuyen_mai` SET `ngay_ket_thuc` = '2099-12-31 23:59:59' WHERE `ngay_ket_thuc` IS NULL;

UPDATE `chuong_trinh_khuyen_mai` SET `trang_thai` = 'DANG_HOAT_DONG'  WHERE `trang_thai` IN ('1', '1.0');
UPDATE `chuong_trinh_khuyen_mai` SET `trang_thai` = 'NGUNG_HOAT_DONG' WHERE `trang_thai` IN ('0', '0.0');

-- ---------------------------------------------------------------------
-- 1.10 chi_tiet_khuyen_mai : bảng ERP dùng hang_hoa_id /
--     hang_hoa_tang_id / so_luong_hang_tang / phan_tram_giam_gia /
--     so_tien_giam; ứng dụng dùng san_pham_id / ma_san_pham /
--     ten_san_pham / don_vi_tinh / loai_giam_gia / gia_tri_giam /
--     so_luong_tang / san_pham_tang_id / ten_san_pham_tang / ghi_chu.
--     => Thêm cột mới, cho phép NULL `hang_hoa_id`/`don_vi_tinh_id`
--        (ứng dụng INSERT không truyền 2 cột này) và chuyển dữ liệu.
-- ---------------------------------------------------------------------
SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'san_pham_id' AS c_name, 'INT(10) UNSIGNED NOT NULL DEFAULT 0' AS c_def UNION ALL
        SELECT 2, 'ma_san_pham', 'VARCHAR(50) NULL' UNION ALL
        SELECT 3, 'ten_san_pham', 'VARCHAR(255) NULL' UNION ALL
        SELECT 4, 'don_vi_tinh', 'VARCHAR(50) NULL' UNION ALL
        SELECT 5, 'loai_giam_gia', 'VARCHAR(20) NOT NULL DEFAULT ''PHAN_TRAM''' UNION ALL
        SELECT 6, 'gia_tri_giam', 'DECIMAL(15,2) NOT NULL DEFAULT 0.00' UNION ALL
        SELECT 7, 'so_luong_tang', 'DECIMAL(15,2) NOT NULL DEFAULT 0.00' UNION ALL
        SELECT 8, 'san_pham_tang_id', 'INT(10) UNSIGNED NULL' UNION ALL
        SELECT 9, 'ten_san_pham_tang', 'VARCHAR(255) NULL' UNION ALL
        SELECT 10, 'ghi_chu', 'VARCHAR(255) NULL'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'chi_tiet_khuyen_mai'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `chi_tiet_khuyen_mai` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT IF(`IS_NULLABLE` = 'NO',
              'ALTER TABLE `chi_tiet_khuyen_mai` MODIFY COLUMN `hang_hoa_id` INT(10) UNSIGNED NULL DEFAULT NULL',
              'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'chi_tiet_khuyen_mai'
      AND COLUMN_NAME = 'hang_hoa_id'
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', @ddl);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT IF(`IS_NULLABLE` = 'NO',
              'ALTER TABLE `chi_tiet_khuyen_mai` MODIFY COLUMN `don_vi_tinh_id` INT(10) UNSIGNED NULL DEFAULT NULL',
              'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'chi_tiet_khuyen_mai'
      AND COLUMN_NAME = 'don_vi_tinh_id'
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', @ddl);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE `chi_tiet_khuyen_mai` SET `san_pham_id` = `hang_hoa_id` WHERE `san_pham_id` = 0 AND `hang_hoa_id` IS NOT NULL;

UPDATE `chi_tiet_khuyen_mai` ct
  JOIN `hang_hoa` hh ON hh.id = ct.hang_hoa_id
  SET ct.ma_san_pham = hh.ma_hang_hoa,
      ct.ten_san_pham = hh.ten_hang_hoa
 WHERE ct.ma_san_pham IS NULL OR ct.ten_san_pham IS NULL;

UPDATE `chi_tiet_khuyen_mai` ct
  JOIN `don_vi_tinh_hang_hoa` dv ON dv.id = ct.don_vi_tinh_id
  SET ct.don_vi_tinh = dv.ten_don_vi_tinh
 WHERE ct.don_vi_tinh IS NULL OR ct.don_vi_tinh = '';

UPDATE `chi_tiet_khuyen_mai` ct
  JOIN `hang_hoa` hh ON hh.id = ct.hang_hoa_tang_id
  SET ct.ten_san_pham_tang = hh.ten_hang_hoa
 WHERE ct.hang_hoa_tang_id IS NOT NULL
   AND (ct.ten_san_pham_tang IS NULL OR ct.ten_san_pham_tang = '');

UPDATE `chi_tiet_khuyen_mai` SET `san_pham_tang_id` = `hang_hoa_tang_id`
 WHERE `san_pham_tang_id` IS NULL AND `hang_hoa_tang_id` IS NOT NULL;

UPDATE `chi_tiet_khuyen_mai` SET `so_luong_tang` = `so_luong_hang_tang`
 WHERE `so_luong_tang` = 0 AND `so_luong_hang_tang` > 0;

UPDATE `chi_tiet_khuyen_mai`
   SET `gia_tri_giam`  = `phan_tram_giam_gia`,
       `loai_giam_gia` = 'PHAN_TRAM'
 WHERE `gia_tri_giam` = 0 AND `phan_tram_giam_gia` > 0;

UPDATE `chi_tiet_khuyen_mai`
   SET `gia_tri_giam`  = `so_tien_giam`,
       `loai_giam_gia` = 'SO_TIEN'
 WHERE `gia_tri_giam` = 0 AND `so_tien_giam` > 0 AND `phan_tram_giam_gia` = 0;


-- ---------------------------------------------------------------------
-- 1.11 hai view tương thích cho báo cáo kỳ kho.
--     Ứng dụng kiểm tra sự tồn tại của bảng `chi_tiet_hoa_don` và
--     `hoa_don` (qua information_schema.TABLES, có tính cả VIEW) để lấy
--     "Số lượng xuất trong kỳ". Trong DB ERP hai bảng này được đặt tên
--     là `chi_tiet_hoa_don_ban_hang` / `hoa_don_ban_hang` và dùng cột thời
--     gian khác (thoi_gian_ban, trang_thai_hoa_don) nên số lượng xuất
--     luôn bằng 0. Tạo VIEW tương ứng để báo cáo lấy đúng số liệu.
-- ---------------------------------------------------------------------
CREATE OR REPLACE VIEW `chi_tiet_hoa_don` AS
SELECT ct.id, ct.hoa_don_id, ct.san_pham_id, ct.ma_san_pham, ct.ten_san_pham,
       ct.don_vi_tinh, ct.so_luong, ct.don_gia, ct.giam_gia, ct.thanh_tien,
       ct.gia_von, ct.ghi_chu
FROM `chi_tiet_hoa_don_ban_hang` ct;

CREATE OR REPLACE VIEW `hoa_don` AS
SELECT hd.id, hd.ma_hoa_don, hd.chi_nhanh_id, hd.khach_hang_id, hd.nguoi_ban_id,
       hd.so_dien_thoai, hd.dia_chi, hd.in_kem_thong_tin,
       hd.thoi_gian_ban AS ngay_tao,
       hd.tong_tien_hang, hd.giam_gia, hd.thu_khac,
       hd.khach_can_tra, hd.khach_da_thanh_toan, hd.con_no,
       hd.tien_khach_dua, hd.tien_thoi_lai,
       hd.phuong_thuc_thanh_toan, hd.trang_thai_thanh_toan,
       hd.trang_thai_hoa_don AS trang_thai,
       hd.ghi_chu, hd.ngay_cap_nhat
FROM `hoa_don_ban_hang` hd;


-- =====================================================================
-- PHẦN 2 : TẠO CÁC BẢNG LỚP TƯƠNG THÍCH
-- =====================================================================

-- 2.1 Chi nhánh ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `chi_nhanh` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_chi_nhanh` varchar(30) NOT NULL,
  `ten_chi_nhanh` varchar(150) NOT NULL,
  `so_dien_thoai` varchar(30) DEFAULT NULL,
  `dia_chi` varchar(255) DEFAULT NULL,
  `phuong_xa` varchar(100) DEFAULT NULL,
  `quan_huyen` varchar(100) DEFAULT NULL,
  `tinh_thanh` varchar(100) DEFAULT NULL,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_chi_nhanh_ma` (`ma_chi_nhanh`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.2 Vai trò & phân quyền người dùng -------------------------------------
CREATE TABLE IF NOT EXISTS `vai_tro` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_vai_tro` varchar(50) NOT NULL,
  `ten_vai_tro` varchar(100) NOT NULL,
  `mo_ta` varchar(255) DEFAULT NULL,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_vai_tro_ma` (`ma_vai_tro`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `nguoi_dung_vai_tro` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nguoi_dung_id` int(10) UNSIGNED NOT NULL,
  `vai_tro_id` int(10) UNSIGNED NOT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nguoi_dung_vai_tro` (`nguoi_dung_id`, `vai_tro_id`),
  KEY `ix_ndvt_vai_tro` (`vai_tro_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.3 Đơn vị tính / Nhóm hàng / Thương hiệu --------------------------------
CREATE TABLE IF NOT EXISTS `don_vi_tinh` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_don_vi` varchar(30) NOT NULL,
  `ten_don_vi` varchar(100) NOT NULL,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_don_vi_tinh_ma` (`ma_don_vi`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `nhom_san_pham` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) NOT NULL,
  `ten_nhom` varchar(150) NOT NULL,
  `nhom_cha_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nhom_san_pham_ma` (`ma_nhom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `thuong_hieu` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_thuong_hieu` varchar(30) NOT NULL,
  `ten_thuong_hieu` varchar(150) NOT NULL,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_thuong_hieu_ma` (`ma_thuong_hieu`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `nhom_khach_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) NOT NULL,
  `ten_nhom` varchar(100) NOT NULL,
  `phan_tram_giam_gia` decimal(5,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nhom_khach_hang_ma` (`ma_nhom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `nhom_nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) NOT NULL,
  `ten_nhom` varchar(100) NOT NULL,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nhom_nha_cung_cap_ma` (`ma_nhom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.4 Sản phẩm & Tồn kho ---------------------------------------------------
CREATE TABLE IF NOT EXISTS `san_pham` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_san_pham` varchar(50) NOT NULL,
  `ma_vach` varchar(100) DEFAULT NULL,
  `ten_san_pham` varchar(255) NOT NULL,
  `nhom_san_pham_id` int(10) UNSIGNED DEFAULT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED DEFAULT NULL,
  `thuong_hieu_id` int(10) UNSIGNED DEFAULT NULL,
  `gia_ban` decimal(15,2) NOT NULL DEFAULT 0.00,
  `gia_von` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ton_kho_excel` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ton_toi_thieu` decimal(15,3) NOT NULL DEFAULT 0.000,
  `ton_toi_da` decimal(15,3) NOT NULL DEFAULT 0.000,
  `khach_dat` decimal(15,2) NOT NULL DEFAULT 0.00,
  `du_kien_het_hang` date DEFAULT NULL,
  `loai_hang_excel` varchar(50) DEFAULT 'Hàng hóa',
  `nhom_hang_3_cap_excel` varchar(255) DEFAULT NULL,
  `vi_tri_kho` varchar(100) DEFAULT NULL,
  `mo_ta` text,
  `ghi_chu` text,
  `hinh_anh` text,
  `co_ban_truc_tiep` tinyint(1) NOT NULL DEFAULT 1,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `dang_kinh_doanh` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_san_pham_ma` (`ma_san_pham`),
  KEY `ix_sp_ten` (`ten_san_pham`),
  KEY `ix_sp_nhom` (`nhom_san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `ton_kho` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `so_luong_ton` decimal(15,3) NOT NULL DEFAULT 0.000,
  `gia_von_binh_quan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_ton_kho_sp_cn` (`san_pham_id`, `chi_nhanh_id`),
  KEY `ix_ton_kho_cn` (`chi_nhanh_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.5 Khách hàng & Nhà cung cấp --------------------------------------------
CREATE TABLE IF NOT EXISTS `khach_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_khach_hang` varchar(30) NOT NULL,
  `ten_khach_hang` varchar(150) NOT NULL,
  `loai_khach_hang` varchar(30) NOT NULL DEFAULT 'CA_NHAN',
  `so_dien_thoai` varchar(30) DEFAULT NULL,
  `ngay_sinh` date DEFAULT NULL,
  `gioi_tinh` varchar(20) DEFAULT NULL,
  `email` varchar(150) DEFAULT NULL,
  `facebook` varchar(150) DEFAULT NULL,
  `dia_chi` varchar(255) DEFAULT NULL,
  `phuong_xa` varchar(100) DEFAULT NULL,
  `quan_huyen` varchar(100) DEFAULT NULL,
  `tinh_thanh` varchar(100) DEFAULT NULL,
  `nhom_khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `ma_so_thue` varchar(50) DEFAULT NULL,
  `ten_nguoi_mua_hoa_don` varchar(150) DEFAULT NULL,
  `ten_cong_ty` varchar(200) DEFAULT NULL,
  `dia_chi_xuat_hoa_don` varchar(255) DEFAULT NULL,
  `ghi_chu` text,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `nguoi_tao_id` int(10) UNSIGNED DEFAULT NULL,
  `co_du_lieu_kiotviet` tinyint(1) NOT NULL DEFAULT 0,
  `no_can_thu_hien_tai` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tong_ban` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tong_ban_tru_tra_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_khach_hang_ma` (`ma_khach_hang`),
  KEY `ix_kh_ten` (`ten_khach_hang`),
  KEY `ix_kh_sdt` (`so_dien_thoai`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nha_cung_cap` varchar(30) NOT NULL,
  `ten_nha_cung_cap` varchar(200) NOT NULL,
  `so_dien_thoai` varchar(30) DEFAULT NULL,
  `email` varchar(150) DEFAULT NULL,
  `dia_chi` varchar(255) DEFAULT NULL,
  `phuong_xa` varchar(100) DEFAULT NULL,
  `quan_huyen` varchar(100) DEFAULT NULL,
  `tinh_thanh` varchar(100) DEFAULT NULL,
  `ma_so_thue` varchar(50) DEFAULT NULL,
  `nguoi_lien_he` varchar(150) DEFAULT NULL,
  `so_tai_khoan` varchar(50) DEFAULT NULL,
  `ngan_hang` varchar(100) DEFAULT NULL,
  `ghi_chu` text,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `nguoi_tao_id` int(10) UNSIGNED DEFAULT NULL,
  `co_du_lieu_kiotviet` tinyint(1) NOT NULL DEFAULT 0,
  `nguoi_tao_ten` varchar(150) DEFAULT NULL,
  `ngay_nhap_excel` datetime DEFAULT NULL,
  `no_can_tra_hien_tai` decimal(15,2) NOT NULL DEFAULT 0.00,
  `no_can_tra_hien_tai_excel` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tong_mua` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tong_mua_tru_tra_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `khu_vuc` varchar(150) DEFAULT NULL,
  `so_cccd` varchar(50) DEFAULT NULL,
  `nhom_nha_cung_cap_id` int(10) UNSIGNED DEFAULT NULL,
  `nhom_nha_cung_cap_ten` varchar(150) DEFAULT NULL,
  `ten_cong_ty` varchar(200) DEFAULT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nha_cung_cap_ma` (`ma_nha_cung_cap`),
  KEY `ix_ncc_ten` (`ten_nha_cung_cap`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.6 Bán hàng & Nhập hàng --------------------------------------------------
CREATE TABLE IF NOT EXISTS `hoa_don_ban_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_hoa_don` varchar(50) NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_ban_id` int(10) UNSIGNED DEFAULT NULL,
  `so_dien_thoai` varchar(50) DEFAULT NULL,
  `dia_chi` text,
  `in_kem_thong_tin` tinyint(1) NOT NULL DEFAULT 0,
  `thoi_gian_ban` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thu_khac` decimal(15,2) NOT NULL DEFAULT 0.00,
  `khach_can_tra` decimal(15,2) NOT NULL DEFAULT 0.00,
  `khach_da_thanh_toan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `con_no` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tien_khach_dua` decimal(15,2) DEFAULT 0.00,
  `tien_thoi_lai` decimal(15,2) DEFAULT 0.00,
  `phuong_thuc_thanh_toan` varchar(50) DEFAULT 'TIEN_MAT',
  `trang_thai_thanh_toan` varchar(30) NOT NULL DEFAULT 'DA_THANH_TOAN',
  `trang_thai_hoa_don` varchar(30) NOT NULL DEFAULT 'HOAN_THANH',
  `ghi_chu` text,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_hoa_don_ma` (`ma_hoa_don`),
  KEY `ix_hdbh_ngay` (`thoi_gian_ban`),
  KEY `ix_hdbh_kh` (`khach_hang_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `chi_tiet_hoa_don_ban_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) DEFAULT NULL,
  `ten_san_pham` varchar(255) NOT NULL,
  `don_vi_tinh` varchar(100) DEFAULT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `don_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `gia_von` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  KEY `ix_cthdbh_hd` (`hoa_don_id`),
  KEY `ix_cthdbh_sp` (`san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `phieu_nhap_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu_nhap` varchar(50) NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `nha_cung_cap_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_nhap_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian_nhap` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `chi_phi_khac` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tong_tien_nhap` decimal(15,2) NOT NULL DEFAULT 0.00,
  `da_thanh_toan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `con_no` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc_thanh_toan` varchar(50) DEFAULT 'TIEN_MAT',
  `so_hoa_don_dau_vao` varchar(50) DEFAULT NULL,
  `ma_dat_hang_nhap` varchar(50) DEFAULT NULL,
  `trang_thai` varchar(30) NOT NULL DEFAULT 'HOAN_THANH',
  `ghi_chu` text,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_phieu_nhap_ma` (`ma_phieu_nhap`),
  KEY `ix_pnh_cn` (`chi_nhanh_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `chi_tiet_phieu_nhap_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `phieu_nhap_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) DEFAULT NULL,
  `ten_san_pham` varchar(255) NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `don_gia_nhap` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  KEY `ix_ctpnh_pn` (`phieu_nhap_id`),
  KEY `ix_ctpnh_sp` (`san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.7 Sổ quỹ (phiếu thu / chi) & nhóm chi phí -----------------------------
CREATE TABLE IF NOT EXISTS `nhom_chi_phi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) NOT NULL,
  `ten_nhom` varchar(150) NOT NULL,
  `ghi_chu` text,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_nhom_chi_phi_ma` (`ma_nhom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `phieu_chi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu_chi` varchar(50) NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `nhom_chi_phi_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc` varchar(30) NOT NULL DEFAULT 'TIEN_MAT',
  `noi_dung` text,
  `nguoi_nop_nhan` varchar(150) DEFAULT NULL,
  `ghi_chu` text,
  `thoi_gian_chi` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `trang_thai` varchar(30) NOT NULL DEFAULT 'HOAN_THANH',
  `hoa_don_ban_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `ma_hoa_don` varchar(50) DEFAULT NULL,
  `phieu_nhap_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `ma_phieu_nhap` varchar(50) DEFAULT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_phieu_chi_ma` (`ma_phieu_chi`),
  KEY `ix_pc_ngay` (`thoi_gian_chi`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.8 Lịch sử công nợ & lịch sử nhập xuất kho ------------------------------
CREATE TABLE IF NOT EXISTS `lich_su_cong_no_khach_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `khach_hang_id` int(10) UNSIGNED NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `loai_giao_dich` varchar(30) NOT NULL,
  `loai_chung_tu` varchar(30) DEFAULT NULL,
  `ma_chung_tu` varchar(50) DEFAULT NULL,
  `ghi_no` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_co` decimal(15,2) NOT NULL DEFAULT 0.00,
  `so_du` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  KEY `ix_lscn_kh` (`khach_hang_id`),
  KEY `ix_lscn_thoi_gian` (`thoi_gian`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `lich_su_cong_no_nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `loai_giao_dich` varchar(30) NOT NULL,
  `loai_chung_tu` varchar(30) DEFAULT NULL,
  `ma_chung_tu` varchar(50) DEFAULT NULL,
  `ghi_no` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_co` decimal(15,2) NOT NULL DEFAULT 0.00,
  `so_du` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  KEY `ix_lscnncc_ncc` (`nha_cung_cap_id`),
  KEY `ix_lscnncc_thoi_gian` (`thoi_gian`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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

-- 2.9 Các bảng phụ trợ khác -------------------------------------------------
CREATE TABLE IF NOT EXISTS `thanh_toan` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_thanh_toan` varchar(50) NOT NULL,
  `hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc` varchar(30) NOT NULL DEFAULT 'TIEN_MAT',
  `ma_giao_dich` varchar(100) DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_thanh_toan_ma` (`ma_thanh_toan`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `thanh_toan_nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_thanh_toan` varchar(50) NOT NULL,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `phieu_nhap_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc` varchar(30) NOT NULL DEFAULT 'TIEN_MAT',
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_thanh_toan_ncc_ma` (`ma_thanh_toan`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `giao_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_giao_hang` varchar(50) NOT NULL,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_giao` varchar(150) DEFAULT NULL,
  `so_dien_thoai_nguoi_giao` varchar(30) DEFAULT NULL,
  `nguoi_nhan` varchar(150) DEFAULT NULL,
  `so_dien_thoai_nguoi_nhan` varchar(30) DEFAULT NULL,
  `dia_chi_giao_hang` varchar(500) DEFAULT NULL,
  `phi_giao_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thoi_gian_du_kien` datetime DEFAULT NULL,
  `thoi_gian_giao` datetime DEFAULT NULL,
  `trang_thai` varchar(30) NOT NULL DEFAULT 'CHO_GIAO',
  `ghi_chu` text,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_giao_hang_ma` (`ma_giao_hang`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.10 Giá riêng cho khách hàng --------------------------------------------
CREATE TABLE IF NOT EXISTS `gia_ban_khach_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `khach_hang_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED DEFAULT NULL,
  `gia_rieng` decimal(15,2) NOT NULL DEFAULT 0.00,
  `chiet_khau` decimal(5,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` varchar(255) DEFAULT NULL,
  `ngay_ap_dung` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_gbkh_khach_sanpham` (`khach_hang_id`, `san_pham_id`),
  KEY `ix_gbkh_khach` (`khach_hang_id`),
  KEY `ix_gbkh_sp` (`san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2.11 Công nợ theo kỳ kế toán --------------------------------------------
CREATE TABLE IF NOT EXISTS `cong_no_khach_hang_ky` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ky_ke_toan_id` int(10) UNSIGNED NOT NULL,
  `khach_hang_id` int(10) UNSIGNED NOT NULL,
  `no_dau_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phat_sinh_trong_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `da_thu_trong_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `no_cuoi_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `so_ky_no` int NOT NULL DEFAULT 1,
  `muc_do_uu_tien` varchar(20) NOT NULL DEFAULT 'TRUNG_BINH',
  `ghi_chu` text,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_cnk_ky_khach` (`ky_ke_toan_id`, `khach_hang_id`),
  KEY `ix_cnk_ky` (`ky_ke_toan_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `cong_no_ncc_ky` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ky_ke_toan_id` int(10) UNSIGNED NOT NULL,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `no_dau_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phat_sinh_trong_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `da_tra_trong_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `no_cuoi_ky` decimal(15,2) NOT NULL DEFAULT 0.00,
  `so_ky_no` int NOT NULL DEFAULT 1,
  `muc_do_uu_tien` varchar(20) NOT NULL DEFAULT 'TRUNG_BINH',
  `ghi_chu` text,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_cnn_ky_ncc` (`ky_ke_toan_id`, `nha_cung_cap_id`),
  KEY `ix_cnn_ky` (`ky_ke_toan_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =====================================================================
-- PHẦN 3 : SEED DỮ LIỆU NỀN TỪ DỮ LIỆU ERP HIỆN CÓ
-- =====================================================================

-- 3.1 Vai trò (mã khớp với hằng số trong Services/Quyen.cs) --------------
INSERT INTO `vai_tro` (`id`, `ma_vai_tro`, `ten_vai_tro`, `mo_ta`, `trang_thai`)
VALUES
  (1, 'QUAN_TRI',  'Quản trị hệ thống', 'Toàn quyền trên hệ thống',   1),
  (2, 'QUAN_LY',   'Quản lý',           'Quản lý vận hành',             1),
  (3, 'THU_NGAN',  'Thu ngân',          'Thu tiền và ghi công nợ',        1),
  (4, 'KHO',       'Nhân viên kho',      'Quản lý kho và tồn kho',       1),
  (5, 'BAO_CAO',   'Xem báo cáo',       'Chỉ xem báo cáo',              1),
  (6, 'ADMIN',     'Quản trị viên',     'Tài khoản quản trị viên',      1),
  (7, 'THU_KHO',   'Thủ kho',            'Xuất kho, trả hàng',           1),
  (8, 'KE_TOAN',   'Kế toán',            'Kế toán và công nợ',            1),
  (9, 'NHAN_VIEN', 'Nhân viên',          'Nhân viên bán hàng',            1)
ON DUPLICATE KEY UPDATE
  `ten_vai_tro` = VALUES(`ten_vai_tro`),
  `mo_ta`      = VALUES(`mo_ta`),
  `trang_thai` = VALUES(`trang_thai`);

-- 3.2 Gán vai trò cho người dùng đang có, theo cột nguoi_dung.vai_tro cũ
INSERT INTO `nguoi_dung_vai_tro` (`nguoi_dung_id`, `vai_tro_id`)
SELECT
  nd.`id`,
  CASE UPPER(COALESCE(nd.`vai_tro`, ''))
    WHEN 'ADMIN'     THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'QUAN_TRI')
    WHEN 'QUAN_TRI'  THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'QUAN_TRI')
    WHEN 'QUAN_LY'   THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'QUAN_LY')
    WHEN 'THU_NGAN'  THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'THU_NGAN')
    WHEN 'THU_KHO'   THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'THU_KHO')
    WHEN 'KHO'       THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'KHO')
    WHEN 'KE_TOAN'   THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'KE_TOAN')
    WHEN 'BAO_CAO'   THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'BAO_CAO')
    WHEN 'NHAN_VIEN' THEN (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'NHAN_VIEN')
    ELSE                  (SELECT `id` FROM `vai_tro` WHERE `ma_vai_tro` = 'NHAN_VIEN')
  END
FROM `nguoi_dung` nd
ON DUPLICATE KEY UPDATE `vai_tro_id` = VALUES(`vai_tro_id`);

-- 3.3 Chi nhánh : lấy theo kho_hang để chi_nhanh_id trùng với kho_hang_id
INSERT INTO `chi_nhanh` (`id`, `ma_chi_nhanh`, `ten_chi_nhanh`, `dia_chi`, `ghi_chu`, `trang_thai`)
SELECT
  kh.`id`,
  kh.`ma_kho_hang`,
  kh.`ten_kho_hang`,
  kh.`dia_chi`,
  kh.`thu_kho`,
  1
FROM `kho_hang` kh
ON DUPLICATE KEY UPDATE
  `ten_chi_nhanh` = VALUES(`ten_chi_nhanh`),
  `dia_chi`      = VALUES(`dia_chi`);

-- 3.4 Mã nhân viên cho các tài khoản ERP cũ chưa có mã -------------------
UPDATE `nguoi_dung`
SET `ma_nguoi_dung` = CONCAT('NV', LPAD(`id`, 4, '0'))
WHERE `ma_nguoi_dung` IS NULL OR TRIM(`ma_nguoi_dung`) = '';

-- 3.5 Đơn vị tính ----------------------------------------------------------
INSERT INTO `don_vi_tinh` (`id`, `ma_don_vi`, `ten_don_vi`, `ghi_chu`, `trang_thai`)
SELECT dvt.`id`, dvt.`ma_don_vi_tinh`, dvt.`ten_don_vi_tinh`, NULL, 1
FROM `don_vi_tinh_hang_hoa` dvt
ON DUPLICATE KEY UPDATE
  `ten_don_vi` = VALUES(`ten_don_vi`),
  `trang_thai` = VALUES(`trang_thai`);

-- 3.6 Nhóm sản phẩm -------------------------------------------------------
INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`)
SELECT nhh.`id`, nhh.`ma_nhom_hang_hoa`, nhh.`ten_nhom_hang_hoa`, nhh.`ghi_chu`, 1
FROM `nhom_hang_hoa` nhh
ON DUPLICATE KEY UPDATE
  `ten_nhom`  = VALUES(`ten_nhom`),
  `ghi_chu`   = VALUES(`ghi_chu`),
  `trang_thai` = VALUES(`trang_thai`);

-- 3.7 Nhóm khách hàng (nhom_doi_tac loại 1) -------------------------------
INSERT INTO `nhom_khach_hang` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`)
SELECT ndt.`id`, ndt.`ma_nhom`, ndt.`ten_nhom`, ndt.`ghi_chu`, 1
FROM `nhom_doi_tac` ndt
WHERE ndt.`loai_doi_tac_id` = 1
ON DUPLICATE KEY UPDATE
  `ten_nhom`   = VALUES(`ten_nhom`),
  `ghi_chu`    = VALUES(`ghi_chu`),
  `trang_thai` = VALUES(`trang_thai`);

-- 3.8 Nhóm nhà cung cấp (nhom_doi_tac loại 2) -----------------------------
INSERT INTO `nhom_nha_cung_cap` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`)
SELECT ndt.`id`, ndt.`ma_nhom`, ndt.`ten_nhom`, ndt.`ghi_chu`, 1
FROM `nhom_doi_tac` ndt
WHERE ndt.`loai_doi_tac_id` = 2
ON DUPLICATE KEY UPDATE
  `ten_nhom`   = VALUES(`ten_nhom`),
  `ghi_chu`    = VALUES(`ghi_chu`),
  `trang_thai` = VALUES(`trang_thai`);

-- 3.9 Khách hàng (doi_tac loại 1) -----------------------------------------
INSERT INTO `khach_hang`
  (`id`, `ma_khach_hang`, `ten_khach_hang`, `so_dien_thoai`, `email`,
   `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `nhom_khach_hang_id`,
   `ma_so_thue`, `ten_cong_ty`, `dia_chi_xuat_hoa_don`, `ghi_chu`,
   `chi_nhanh_id`, `trang_thai`)
SELECT
  dt.`id`,
  dt.`ma_doi_tac`,
  dt.`ten_doi_tac`,
  dt.`lien_he`,
  dt.`email`,
  dt.`dia_chi`,
  dt.`phuong_xa`,
  dt.`quan_huyen`,
  dt.`tinh_thanh`,
  dt.`nhom_doi_tac_id`,
  dt.`ma_so_thue`,
  dt.`nhan_hieu`,
  dt.`dia_chi_chi_tiet`,
  dt.`ghi_chu`,
  1,
  dt.`trang_thai`
FROM `doi_tac` dt
WHERE dt.`loai_doi_tac_id` = 1
ON DUPLICATE KEY UPDATE
  `ten_khach_hang`       = VALUES(`ten_khach_hang`),
  `so_dien_thoai`        = VALUES(`so_dien_thoai`),
  `email`                = VALUES(`email`),
  `dia_chi`              = VALUES(`dia_chi`),
  `phuong_xa`            = VALUES(`phuong_xa`),
  `quan_huyen`           = VALUES(`quan_huyen`),
  `tinh_thanh`           = VALUES(`tinh_thanh`),
  `nhom_khach_hang_id`   = VALUES(`nhom_khach_hang_id`),
  `ma_so_thue`           = VALUES(`ma_so_thue`),
  `ten_cong_ty`          = VALUES(`ten_cong_ty`),
  `dia_chi_xuat_hoa_don` = VALUES(`dia_chi_xuat_hoa_don`),
  `ghi_chu`              = VALUES(`ghi_chu`),
  `trang_thai`           = VALUES(`trang_thai`);

-- 3.10 Nhà cung cấp (doi_tac loại 2) --------------------------------------
INSERT INTO `nha_cung_cap`
  (`id`, `ma_nha_cung_cap`, `ten_nha_cung_cap`, `so_dien_thoai`, `email`,
   `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `ma_so_thue`,
   `nguoi_lien_he`, `so_tai_khoan`, `ngan_hang`, `ghi_chu`,
   `nhom_nha_cung_cap_id`, `nhom_nha_cung_cap_ten`, `ten_cong_ty`,
   `chi_nhanh_id`, `trang_thai`)
SELECT
  dt.`id`,
  dt.`ma_doi_tac`,
  dt.`ten_doi_tac`,
  dt.`lien_he`,
  dt.`email`,
  dt.`dia_chi`,
  dt.`phuong_xa`,
  dt.`quan_huyen`,
  dt.`tinh_thanh`,
  dt.`ma_so_thue`,
  dt.`lien_he`,
  dt.`so_tai_khoan`,
  dt.`ngan_hang`,
  dt.`ghi_chu`,
  dt.`nhom_doi_tac_id`,
  ndt.`ten_nhom`,
  dt.`nhan_hieu`,
  1,
  dt.`trang_thai`
FROM `doi_tac` dt
LEFT JOIN `nhom_doi_tac` ndt ON ndt.`id` = dt.`nhom_doi_tac_id`
WHERE dt.`loai_doi_tac_id` = 2
ON DUPLICATE KEY UPDATE
  `ten_nha_cung_cap`      = VALUES(`ten_nha_cung_cap`),
  `so_dien_thoai`         = VALUES(`so_dien_thoai`),
  `email`                 = VALUES(`email`),
  `dia_chi`               = VALUES(`dia_chi`),
  `phuong_xa`             = VALUES(`phuong_xa`),
  `quan_huyen`            = VALUES(`quan_huyen`),
  `tinh_thanh`            = VALUES(`tinh_thanh`),
  `ma_so_thue`            = VALUES(`ma_so_thue`),
  `nguoi_lien_he`         = VALUES(`nguoi_lien_he`),
  `ghi_chu`               = VALUES(`ghi_chu`),
  `nhom_nha_cung_cap_id`  = VALUES(`nhom_nha_cung_cap_id`),
  `nhom_nha_cung_cap_ten` = VALUES(`nhom_nha_cung_cap_ten`),
  `ten_cong_ty`           = VALUES(`ten_cong_ty`),
  `trang_thai`            = VALUES(`trang_thai`);

-- 3.11 Sản phẩm : mỗi hàng hóa lấy đúng 1 đơn vị tính (cap = 1, MIN id)
INSERT INTO `san_pham`
  (`id`, `ma_san_pham`, `ma_vach`, `ten_san_pham`, `nhom_san_pham_id`,
   `don_vi_tinh_id`, `gia_ban`, `gia_von`, `ton_kho_excel`, `ton_toi_thieu`,
   `ton_toi_da`, `ghi_chu`, `co_ban_truc_tiep`, `trang_thai`, `dang_kinh_doanh`)
SELECT
  hh.`id`,
  hh.`ma_hang_hoa`,
  hh.`barcode`,
  hh.`ten_hang_hoa`,
  hh.`nhom_hang_hoa_id`,
  ct.`don_vi_tinh_id`,
  COALESCE(ct.`gia_le`, 0),
  COALESCE(ct.`gia_nhap`, 0),
  0,
  hh.`du_tru_min`,
  hh.`du_tru_max`,
  hh.`ghi_chu`,
  hh.`in_an`,
  hh.`trang_thai`,
  hh.`trang_thai`
FROM `hang_hoa` hh
LEFT JOIN `chi_tiet_don_vi_tinh_cua_hang_hoa` ct
  ON ct.`id` = (
       SELECT MIN(c2.`id`)
       FROM `chi_tiet_don_vi_tinh_cua_hang_hoa` c2
       WHERE c2.`hang_hoa_id` = hh.`id`
         AND c2.`cap_don_vi_tinh` = 1)
ON DUPLICATE KEY UPDATE
  `ten_san_pham`      = VALUES(`ten_san_pham`),
  `ma_vach`           = VALUES(`ma_vach`),
  `nhom_san_pham_id`  = VALUES(`nhom_san_pham_id`),
  `don_vi_tinh_id`    = VALUES(`don_vi_tinh_id`),
  `gia_ban`           = VALUES(`gia_ban`),
  `gia_von`           = VALUES(`gia_von`),
  `ton_toi_thieu`     = VALUES(`ton_toi_thieu`),
  `ton_toi_da`        = VALUES(`ton_toi_da`),
  `co_ban_truc_tiep`  = VALUES(`co_ban_truc_tiep`),
  `trang_thai`        = VALUES(`trang_thai`),
  `dang_kinh_doanh`   = VALUES(`dang_kinh_doanh`);

-- 3.12 Tồn kho : theo kho hàng id 1 (Kho Tổng) -------------------------
INSERT INTO `ton_kho`
  (`chi_nhanh_id`, `san_pham_id`, `so_luong_ton`, `gia_von_binh_quan`)
SELECT
  1,
  hh.`id`,
  COALESCE(v.`ton_kho`, 0),
  COALESCE(ct.`gia_nhap`, 0)
FROM `hang_hoa` hh
LEFT JOIN `v_ton_kho_hien_tai` v
  ON v.`hang_hoa_id` = hh.`id` AND v.`kho_hang_id` = 1
LEFT JOIN `chi_tiet_don_vi_tinh_cua_hang_hoa` ct
  ON ct.`id` = (
       SELECT MIN(c2.`id`)
       FROM `chi_tiet_don_vi_tinh_cua_hang_hoa` c2
       WHERE c2.`hang_hoa_id` = hh.`id`
         AND c2.`cap_don_vi_tinh` = 1)
ON DUPLICATE KEY UPDATE
  `so_luong_ton`        = VALUES(`so_luong_ton`),
  `gia_von_binh_quan`   = VALUES(`gia_von_binh_quan`);

-- 3.13 Nhóm chi phí -------------------------------------------------------
INSERT INTO `nhom_chi_phi` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`)
VALUES
  (1, 'DIEN_NUOC',        'Điện nước',         NULL, 1),
  (2, 'VAN_CHUYEN',       'Vận chuyển',        NULL, 1),
  (3, 'BAO_BI',           'Bao bì',            NULL, 1),
  (4, 'HU_HAO',           'Hư hao',            NULL, 1),
  (5, 'LUONG',            'Lương',             NULL, 1),
  (6, 'KHAC',             'Chi phí khác',      NULL, 1),
  (7, 'THU_KHACH_HANG',   'Thu khách hàng',    NULL, 1),
  (8, 'CHI_NHA_CUNG_CAP', 'Chi nhà cung cấp',  NULL, 1)
ON DUPLICATE KEY UPDATE
  `ten_nhom`   = VALUES(`ten_nhom`),
  `trang_thai` = VALUES(`trang_thai`);


-- =====================================================================
-- PHẦN 4 : KIỂM TRA SAU KHI CHẠY
-- =====================================================================
SELECT 'chi_nhanh' AS bang, COUNT(*) AS so_dong FROM `chi_nhanh`
UNION ALL SELECT 'vai_tro',          COUNT(*) FROM `vai_tro`
UNION ALL SELECT 'nguoi_dung_vai_tro',COUNT(*) FROM `nguoi_dung_vai_tro`
UNION ALL SELECT 'don_vi_tinh',      COUNT(*) FROM `don_vi_tinh`
UNION ALL SELECT 'nhom_san_pham',    COUNT(*) FROM `nhom_san_pham`
UNION ALL SELECT 'nhom_khach_hang',  COUNT(*) FROM `nhom_khach_hang`
UNION ALL SELECT 'nhom_nha_cung_cap',COUNT(*) FROM `nhom_nha_cung_cap`
UNION ALL SELECT 'san_pham',         COUNT(*) FROM `san_pham`
UNION ALL SELECT 'ton_kho',          COUNT(*) FROM `ton_kho`
UNION ALL SELECT 'khach_hang',       COUNT(*) FROM `khach_hang`
UNION ALL SELECT 'nha_cung_cap',     COUNT(*) FROM `nha_cung_cap`
UNION ALL SELECT 'hoa_don_ban_hang', COUNT(*) FROM `hoa_don_ban_hang`
UNION ALL SELECT 'phieu_nhap_hang',  COUNT(*) FROM `phieu_nhap_hang`
UNION ALL SELECT 'phieu_chi',        COUNT(*) FROM `phieu_chi`
UNION ALL SELECT 'nhom_chi_phi',     COUNT(*) FROM `nhom_chi_phi`
UNION ALL SELECT 'lich_su_nhap_xuat_kho', COUNT(*) FROM `lich_su_nhap_xuat_kho`;

SELECT nd.`id`, nd.`ma_nguoi_dung`, nd.`ten_dang_nhap`, nd.`ho_ten`,
       nd.`chi_nhanh_id`, cn.`ten_chi_nhanh`,
       GROUP_CONCAT(vt.`ma_vai_tro` ORDER BY vt.`id` SEPARATOR ',') AS ma_vai_tro
FROM `nguoi_dung` nd
LEFT JOIN `chi_nhanh` cn ON cn.`id` = nd.`chi_nhanh_id`
LEFT JOIN `nguoi_dung_vai_tro` ndvt ON ndvt.`nguoi_dung_id` = nd.`id`
LEFT JOIN `vai_tro` vt ON vt.`id` = ndvt.`vai_tro_id`
GROUP BY nd.`id`, nd.`ma_nguoi_dung`, nd.`ten_dang_nhap`, nd.`ho_ten`,
         nd.`chi_nhanh_id`, cn.`ten_chi_nhanh`
ORDER BY nd.`id`;

SELECT sp.`id`, sp.`ma_san_pham`, sp.`ten_san_pham`, sp.`nhom_san_pham_id`,
       sp.`don_vi_tinh_id`, sp.`gia_ban`, sp.`gia_von`
FROM `san_pham` sp
ORDER BY sp.`id`;
