-- =====================================================================
-- MIGRATION: BỔ SUNG BẢNG DANH MỤC NHÓM HÀNG & ĐƠN VỊ TÍNH
-- ---------------------------------------------------------------------
-- Áp dụng cho : database đang kết nối (mặc định `shophoa_Huy_tiemhoatest`
--                và `shophoa_Huy_tiemhoatest_DLTrang`)
-- Ngày        : 2026-10-22
-- ---------------------------------------------------------------------
-- MỤC ĐÍCH
--   Ứng dụng .NET đọc/ghi danh mục ở hai bảng:
--       nhom_san_pham  (id, ma_nhom, ten_nhom, nhom_cha_id, ghi_chu, trang_thai)
--       don_vi_tinh    (id, ma_don_vi, ten_don_vi, ghi_chu, trang_thai)
--   (định nghĩa giống mục 7.6.3 của database/shophoa_Huy_tiemhoatest_fixed.sql)
--
--   Database ERP gốc chỉ có bảng:
--       nhom_hang_hoa        (ma_nhom_hang_hoa, ten_nhom_hang_hoa)
--       don_vi_tinh_hang_hoa (ma_don_vi_tinh, ten_don_vi_tinh)
--   nên khi mở "Cài đặt danh mục -> Quản lý Nhóm hàng & Đơn vị tính" và
--   bấm thêm, MySQL báo lỗi 1146:
--       Table '<database>.nhom_san_pham' doesn't exist
--
-- NGUYÊN TẮC
--   * Idempotent: chạy lại bao nhiêu lần cũng an toàn.
--   * KHÔNG DROP / TRUNCATE / DELETE: không mất dữ liệu ERP hay dữ liệu
--     danh mục đã có trong bảng đích.
--   * Chỉ seed khi bảng đích vừa được tạo hoặc đang rỗng, để không ghi đè
--     những mục người dùng đã thêm/sửa.
--   * MariaDB 10.1 không có "ADD COLUMN IF NOT EXISTS" nên phần bổ sung cột
--     dùng information_schema + PREPARE/EXECUTE.
--   * Không dùng stored procedure / DELIMITER để file chạy được trên cả
--     mysql CLI, phpMyAdmin và MySqlConnector (multi-statement).
--
-- CÁCH CHẠY
--   mysql -h <host> -u <user> -p <database> < migration_danh_muc_nhom_hang_don_vi_tinh.sql
--   (hoặc dùng Database > Import trong phpMyAdmin)
--
-- LƯU Ý: DDL trong MySQL/MariaDB tự commit, không thể rollback.
-- =====================================================================

SET NAMES utf8mb4;
SET SESSION group_concat_max_len = 1048576;

-- KHÔNG tự ý đổi database đang kết nối. Nếu muốn chạy cho database cụ thể
-- thì bỏ comment dòng dưới (giữ nguyên đúng tên database):
-- USE `shophoa_Huy_tiemhoatest_DLTrang`;


-- =====================================================================
-- PHẦN 1 : TẠO BẢNG `don_vi_tinh` NẾU CHƯA CÓ
-- =====================================================================

SET @bang_don_vi_tinh_da_co = (
    SELECT COUNT(*)
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'don_vi_tinh'
);

SET @ddl = 'SELECT 1';
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT IF(@bang_don_vi_tinh_da_co = 0,
        'CREATE TABLE IF NOT EXISTS `don_vi_tinh` (
            `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
            `ma_don_vi` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
            `ten_don_vi` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
            `ghi_chu` text COLLATE utf8mb4_unicode_ci,
            `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            KEY `idx_don_vi_tinh_ten` (`ten_don_vi`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci',
        'SELECT 1')
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;


-- =====================================================================
-- PHẦN 2 : TẠO BẢNG `nhom_san_pham` NẾU CHƯA CÓ
-- =====================================================================

SET @bang_nhom_san_pham_da_co = (
    SELECT COUNT(*)
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'nhom_san_pham'
);

SET @ddl = (
    SELECT IF(@bang_nhom_san_pham_da_co = 0,
        'CREATE TABLE IF NOT EXISTS `nhom_san_pham` (
            `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
            `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
            `ten_nhom` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
            `nhom_cha_id` int(10) UNSIGNED DEFAULT NULL,
            `ghi_chu` text COLLATE utf8mb4_unicode_ci,
            `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
            PRIMARY KEY (`id`),
            KEY `idx_nhom_san_pham_ten` (`ten_nhom`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci',
        'SELECT 1')
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;


-- =====================================================================
-- PHẦN 3 : BỔ SUNG CỘT CÒN THIẾU (bảng đã có nhưng shape cũ)
-- =====================================================================

SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ma_don_vi' AS c_name, 'varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''''' AS c_def UNION ALL
        SELECT 2, 'ten_don_vi', 'varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''''' UNION ALL
        SELECT 3, 'ghi_chu',   'text COLLATE utf8mb4_unicode_ci NULL' UNION ALL
        SELECT 4, 'trang_thai','tinyint(1) NOT NULL DEFAULT 1'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'don_vi_tinh'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `don_vi_tinh` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @ddl = (
    SELECT GROUP_CONCAT(
             CONCAT('ADD COLUMN `', c.c_name, '` ', c.c_def)
             ORDER BY c.c_ord SEPARATOR ', ')
    FROM (
        SELECT 1 AS c_ord, 'ma_nhom' AS c_name, 'varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''''' AS c_def UNION ALL
        SELECT 2, 'ten_nhom',  'varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT ''''' UNION ALL
        SELECT 3, 'nhom_cha_id', 'int(10) UNSIGNED NULL' UNION ALL
        SELECT 4, 'ghi_chu',   'text COLLATE utf8mb4_unicode_ci NULL' UNION ALL
        SELECT 5, 'trang_thai','tinyint(1) NOT NULL DEFAULT 1'
    ) c
    LEFT JOIN information_schema.COLUMNS ic
      ON ic.TABLE_SCHEMA = DATABASE()
     AND ic.TABLE_NAME = 'nhom_san_pham'
     AND ic.COLUMN_NAME = c.c_name
    WHERE ic.COLUMN_NAME IS NULL
);
SET @ddl = IF(@ddl IS NULL, 'SELECT 1', CONCAT('ALTER TABLE `nhom_san_pham` ', @ddl));
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 3.3 Điền mã cho các bản ghi cũ bị trống mã (tránh đụng UNIQUE khi thêm mới)
UPDATE `don_vi_tinh`
SET `ma_don_vi` = CONCAT('DVT', LPAD(`id`, 8, '0'))
WHERE `ma_don_vi` IS NULL OR `ma_don_vi` = '';

UPDATE `nhom_san_pham`
SET `ma_nhom` = CONCAT('NH', LPAD(`id`, 8, '0'))
WHERE `ma_nhom` IS NULL OR `ma_nhom` = '';


-- =====================================================================
-- PHẦN 4 : SAO CHÉP DANH MỤC CŨ TỪ BẢNG ERP (bảo toàn dữ liệu)
--   Chỉ chạy khi bảng đích vừa được tạo hoặc đang rỗng.
-- =====================================================================

-- 4.1 Đơn vị tính : don_vi_tinh_hang_hoa -> don_vi_tinh
SET @so_dong_don_vi_tinh = (SELECT COUNT(*) FROM `don_vi_tinh`);
SET @co_bang_erp_dvt = (
    SELECT COUNT(*)
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'don_vi_tinh_hang_hoa'
);
SET @co_cot_erp_dvt = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'don_vi_tinh_hang_hoa'
      AND COLUMN_NAME IN ('ma_don_vi_tinh', 'ten_don_vi_tinh')
);

SET @ddl = (
    SELECT IF(@co_bang_erp_dvt > 0 AND @co_cot_erp_dvt = 2
              AND (@bang_don_vi_tinh_da_co = 0 OR @so_dong_don_vi_tinh = 0),
        'INSERT INTO `don_vi_tinh` (`id`, `ma_don_vi`, `ten_don_vi`, `trang_thai`)
         SELECT dvt.`id`, dvt.`ma_don_vi_tinh`, dvt.`ten_don_vi_tinh`, 1
         FROM `don_vi_tinh_hang_hoa` dvt
         ON DUPLICATE KEY UPDATE `ten_don_vi` = VALUES(`ten_don_vi`)',
        'SELECT 1')
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 4.2 Nhóm hàng : nhom_hang_hoa -> nhom_san_pham
SET @so_dong_nhom_san_pham = (SELECT COUNT(*) FROM `nhom_san_pham`);
SET @co_bang_erp_nhom = (
    SELECT COUNT(*)
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'nhom_hang_hoa'
);
SET @co_cot_erp_nhom = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'nhom_hang_hoa'
      AND COLUMN_NAME IN ('ma_nhom_hang_hoa', 'ten_nhom_hang_hoa')
);
SET @co_cot_ghi_chu_nhom = (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'nhom_hang_hoa'
      AND COLUMN_NAME = 'ghi_chu'
);

SET @ddl = (
    SELECT IF(@co_bang_erp_nhom > 0 AND @co_cot_erp_nhom = 2
              AND (@bang_nhom_san_pham_da_co = 0 OR @so_dong_nhom_san_pham = 0),
        IF(@co_cot_ghi_chu_nhom > 0,
            'INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`)
             SELECT nhh.`id`, nhh.`ma_nhom_hang_hoa`, nhh.`ten_nhom_hang_hoa`, nhh.`ghi_chu`, 1
             FROM `nhom_hang_hoa` nhh
             ON DUPLICATE KEY UPDATE
                `ten_nhom` = VALUES(`ten_nhom`),
                `ghi_chu`  = VALUES(`ghi_chu`)',
            'INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `trang_thai`)
             SELECT nhh.`id`, nhh.`ma_nhom_hang_hoa`, nhh.`ten_nhom_hang_hoa`, 1
             FROM `nhom_hang_hoa` nhh
             ON DUPLICATE KEY UPDATE `ten_nhom` = VALUES(`ten_nhom`)'
        ),
        'SELECT 1')
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;


-- =====================================================================
-- PHẦN 5 : KIỂM TRA SAU KHI CHẠY
-- =====================================================================

SELECT 'don_vi_tinh'   AS bang, COUNT(*) AS so_dong FROM `don_vi_tinh`
UNION ALL SELECT 'nhom_san_pham', COUNT(*) FROM `nhom_san_pham`;

SELECT `id`, `ma_nhom`, `ten_nhom`, `nhom_cha_id`, `trang_thai`
FROM `nhom_san_pham`
ORDER BY `id`;

SELECT `id`, `ma_don_vi`, `ten_don_vi`, `trang_thai`
FROM `don_vi_tinh`
ORDER BY `id`;
