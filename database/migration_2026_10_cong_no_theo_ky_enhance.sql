-- Migration: nâng cấp quản lý công nợ khách hàng theo kỳ
-- Chạy một lần trên database hiện có. Script an toàn khi chạy lại.

SET @schema := DATABASE();

-- 1. Bảng chốt công nợ theo kỳ: thêm cột điều chỉnh trong kỳ.
SET @sql := (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE cong_no_khach_hang_ky ADD COLUMN dieu_chinh_trong_ky DECIMAL(15,2) NOT NULL DEFAULT 0 AFTER phat_sinh_trong_ky',
        'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'cong_no_khach_hang_ky'
      AND COLUMN_NAME = 'dieu_chinh_trong_ky'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 2. Audit kết kỳ / mở lại kỳ.
SET @sql := (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE ky_ke_toan ADD COLUMN ly_do_mo_lai TEXT NULL',
        'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'ky_ke_toan'
      AND COLUMN_NAME = 'ly_do_mo_lai'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE ky_ke_toan ADD COLUMN nguoi_mo_lai_id INT UNSIGNED NULL',
        'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'ky_ke_toan'
      AND COLUMN_NAME = 'nguoi_mo_lai_id'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE ky_ke_toan ADD COLUMN ngay_mo_lai DATETIME NULL',
        'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'ky_ke_toan'
      AND COLUMN_NAME = 'ngay_mo_lai'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 3. Nợ đầu kỳ: bổ sung loại đối tác và ngày áp dụng (nếu database cũ chưa có).
SET @sql := (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE chi_tiet_cong_no_dau_ky ADD COLUMN loai_doi_tac VARCHAR(20) NOT NULL DEFAULT '''' AFTER chi_nhanh_id',
        'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'chi_tiet_cong_no_dau_ky'
      AND COLUMN_NAME = 'loai_doi_tac'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
    SELECT IF(COUNT(*) = 0,
        'ALTER TABLE chi_tiet_cong_no_dau_ky ADD COLUMN ngay_dau_ky DATE NULL',
        'SELECT 1')
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = 'chi_tiet_cong_no_dau_ky'
      AND COLUMN_NAME = 'ngay_dau_ky'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- 4. Bảng điều chỉnh công nợ (tạo nếu database cũ chưa có).
CREATE TABLE IF NOT EXISTS dieu_chinh_cong_no (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    ma_dieu_chinh VARCHAR(80) NOT NULL,
    khach_hang_id INT UNSIGNED NULL,
    nha_cung_cap_id INT UNSIGNED NULL,
    chi_nhanh_id INT UNSIGNED NOT NULL DEFAULT 1,
    so_tien DECIMAL(15,2) NOT NULL DEFAULT 0,
    loai_dieu_chinh VARCHAR(30) NOT NULL,
    ly_do TEXT NULL,
    nguoi_thuc_hien_id INT UNSIGNED NULL,
    thoi_gian DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_ma_dieu_chinh (ma_dieu_chinh),
    INDEX idx_dc_khach (khach_hang_id),
    INDEX idx_dc_thoi_gian (thoi_gian)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. Cấu hình ngưỡng ưu tiên nhắc nợ.
CREATE TABLE IF NOT EXISTS cau_hinh_cong_no (
    id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    khoa VARCHAR(80) NOT NULL,
    gia_tri DECIMAL(15,2) NOT NULL DEFAULT 0,
    mo_ta VARCHAR(255) NULL,
    ngay_cap_nhat DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uk_cau_hinh_cong_no (khoa)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO cau_hinh_cong_no (khoa, gia_tri, mo_ta)
VALUES
    ('uu_tien_cao_so_ky', 3, 'Số kỳ nợ liên tiếp để xếp mức ưu tiên cao'),
    ('uu_tien_cao_so_tien', 5000000, 'Số tiền nợ để xếp mức ưu tiên cao'),
    ('uu_tien_trung_binh_so_ky', 2, 'Số kỳ nợ liên tiếp để xếp mức ưu tiên trung bình'),
    ('uu_tien_trung_binh_so_tien', 2000000, 'Số tiền nợ để xếp mức ưu tiên trung bình')
ON DUPLICATE KEY UPDATE mo_ta = VALUES(mo_ta);
