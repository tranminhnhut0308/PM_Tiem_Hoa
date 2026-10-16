-- ========================================================
-- CƠ SỞ DỮ LIỆU: QUẢN LÝ SHOP HOA TƯƠI
-- Tương thích: MySQL 8.0+ / MariaDB 10.5+
-- Bảng mã: UTF8MB4 (Hỗ trợ tiếng Việt đầy đủ)
-- LƯU Ý: Script DROP và tạo lại các bảng trong database được chọn.
-- Hãy sao lưu trước khi chạy nếu database đã có dữ liệu cần giữ.
-- ========================================================

CREATE DATABASE IF NOT EXISTS `shophoa_Huy_tiemhoatest`
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `shophoa_Huy_tiemhoatest`;

SET FOREIGN_KEY_CHECKS = 0;
SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
SET time_zone = "+07:00";

-- Xóa các bảng cũ nếu đã tồn tại để tránh xung đột
DROP TABLE IF EXISTS `phieu_thu_chi`;
DROP TABLE IF EXISTS `vai_tro_quyen`;
DROP TABLE IF EXISTS `quyen_he_thong`;
DROP TABLE IF EXISTS `loai_phieu_thu_chi`;
DROP TABLE IF EXISTS `chi_tiet_phieu_nhap_xuat`;
DROP TABLE IF EXISTS `phieu_nhap_xuat_hang_hoa`;
DROP TABLE IF EXISTS `loai_phieu_nhap_xuat`;
DROP TABLE IF EXISTS `chi_tiet_cong_no_dau_ky`;
DROP TABLE IF EXISTS `chi_tiet_hang_hoa_dau_ky`;
DROP TABLE IF EXISTS `so_quy_dau_ky`;
DROP TABLE IF EXISTS `ky_ke_toan`;
DROP TABLE IF EXISTS `chi_tiet_khuyen_mai`;
DROP TABLE IF EXISTS `chuong_trinh_khuyen_mai`;
DROP TABLE IF EXISTS `chi_tiet_gia_doi_tac`;
DROP TABLE IF EXISTS `doi_tac`;
DROP TABLE IF EXISTS `nhom_doi_tac`;
DROP TABLE IF EXISTS `loai_doi_tac`;
DROP TABLE IF EXISTS `chi_tiet_don_vi_tinh_cua_hang_hoa`;
DROP TABLE IF EXISTS `hang_hoa`;
DROP TABLE IF EXISTS `don_vi_tinh_hang_hoa`;
DROP TABLE IF EXISTS `nhom_hang_hoa`;
DROP TABLE IF EXISTS `kho_hang`;
DROP TABLE IF EXISTS `don_vi`;
DROP TABLE IF EXISTS `nhat_ky_he_thong`;
DROP TABLE IF EXISTS `nguoi_dung`;

SET FOREIGN_KEY_CHECKS = 1;

-- --------------------------------------------------------
-- 1. HỆ THỐNG & TÀI KHOẢN NGƯỜI DÙNG
-- --------------------------------------------------------

CREATE TABLE `nguoi_dung` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ten_dang_nhap` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `mat_khau` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ho_ten` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `vai_tro` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'NHAN_VIEN', -- ADMIN, THU_NGAN, KE_TOAN, THU_KHO
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Không tạo sẵn tài khoản với mật khẩu chữ thường.
-- Tạo tài khoản đầu tiên từ màn hình quản trị; lưu mật khẩu đã băm BCrypt.

CREATE TABLE `quyen_he_thong` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_quyen` varchar(80) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_quyen` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nhom_chuc_nang` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `vai_tro_quyen` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `vai_tro` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `quyen_id` int(10) UNSIGNED NOT NULL,
  `duoc_xem` tinyint(1) NOT NULL DEFAULT 0,
  `duoc_them` tinyint(1) NOT NULL DEFAULT 0,
  `duoc_sua` tinyint(1) NOT NULL DEFAULT 0,
  `duoc_xoa` tinyint(1) NOT NULL DEFAULT 0,
  `duoc_xuat` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_vai_tro_quyen` (`vai_tro`, `quyen_id`),
  CONSTRAINT `fk_vtq_quyen` FOREIGN KEY (`quyen_id`) REFERENCES `quyen_he_thong` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `nhat_ky_he_thong` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nguoi_dung_id` int(10) UNSIGNED DEFAULT NULL,
  `hanh_dong` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `bang_anh_huong` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ban_ghi_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `fk_log_user` (`nguoi_dung_id`),
  CONSTRAINT `fk_log_user` FOREIGN KEY (`nguoi_dung_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------
-- 2. DANH MỤC CƠ SỞ (ĐƠN VỊ, KHO HÀNG, ĐỐI TÁC, HÀNG HÓA)
-- --------------------------------------------------------

CREATE TABLE `don_vi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_don_vi` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_don_vi` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `website` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `lien_he` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ma_so_thue` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ke_toan` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thu_truong` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thu_quy` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_tai_khoan` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ngan_hang` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `logo` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `don_vi` (`id`, `ma_don_vi`, `ten_don_vi`, `dia_chi`, `lien_he`, `tinh_thanh`, `trang_thai`) VALUES
(1, 'DV01', 'SHOP HOATUOI', 'Ninh Kiều, Cần Thơ', '0901 000 001', 'Cần Thơ', 1);

CREATE TABLE `kho_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_kho_hang` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_kho_hang` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `don_vi_id` int(10) UNSIGNED NOT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thu_kho` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_kho_don_vi` (`don_vi_id`),
  CONSTRAINT `fk_kho_don_vi` FOREIGN KEY (`don_vi_id`) REFERENCES `don_vi` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `kho_hang` (`id`, `ma_kho_hang`, `ten_kho_hang`, `don_vi_id`) VALUES
(1, 'K01', 'Kho Tổng', 1),
(2, 'K02', 'Kho Trưng Bày', 1);

CREATE TABLE `nhom_hang_hoa` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom_hang_hoa` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_nhom_hang_hoa` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nhom_hang_hoa` (`id`, `ma_nhom_hang_hoa`, `ten_nhom_hang_hoa`) VALUES
(1, 'HOA_BO', 'Hoa bó nghệ thuật'),
(2, 'HOA_CHUC_MUNG', 'Hoa chúc mừng / Khai trương'),
(3, 'HOA_CUOI', 'Hoa cưới cầm tay'),
(4, 'HOA_LE', 'Hoa cành / Phụ liệu');

CREATE TABLE `don_vi_tinh_hang_hoa` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_don_vi_tinh` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_don_vi_tinh` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `don_vi_tinh_hang_hoa` (`id`, `ma_don_vi_tinh`, `ten_don_vi_tinh`) VALUES
(1, 'CANH', 'Cành'),
(2, 'BO', 'Bó'),
(3, 'KE', 'Kệ'),
(4, 'GIO', 'Giỏ'),
(5, 'CHAU', 'Chậu');

CREATE TABLE `hang_hoa` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nhom_hang_hoa_id` int(10) UNSIGNED NOT NULL,
  `ma_hang_hoa` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_hang_hoa` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `barcode` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quy_cach` text COLLATE utf8mb4_unicode_ci,
  `trong_luong` decimal(15,3) DEFAULT '0.000',
  `du_tru_min` decimal(15,3) NOT NULL DEFAULT '0.000',
  `du_tru_max` decimal(15,3) NOT NULL DEFAULT '0.000',
  `in_an` tinyint(1) NOT NULL DEFAULT 1,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1, -- Còn sử dụng
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_hh_nhomhh` (`nhom_hang_hoa_id`),
  CONSTRAINT `fk_hh_nhomhh` FOREIGN KEY (`nhom_hang_hoa_id`) REFERENCES `nhom_hang_hoa` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `hang_hoa` (`id`, `nhom_hang_hoa_id`, `ma_hang_hoa`, `ten_hang_hoa`, `barcode`, `quy_cach`, `in_an`, `trang_thai`) VALUES
(1, 4, 'HH001', 'Hoa hồng đỏ Đà Lạt', '893000000001', 'Cành dài 60cm', 1, 1),
(2, 1, 'HH002', 'Bó hoa Tulip Hồng', '893000000002', 'Bó 10 bông kèm phụ kiện', 1, 1),
(3, 2, 'HH003', 'Kệ hoa khai trương Đại Cát', '893000000003', 'Kệ 2 tầng', 1, 1);

CREATE TABLE `chi_tiet_don_vi_tinh_cua_hang_hoa` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `hang_hoa_id` int(10) UNSIGNED NOT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED NOT NULL,
  `cap_don_vi_tinh` tinyint(1) NOT NULL DEFAULT 1, -- 1: Đơn vị gốc, 2: Cấp 2, 3: Cấp 3
  `ty_le_quy_doi` decimal(15,3) NOT NULL DEFAULT 1.000,
  `gia_nhap` decimal(15,2) NOT NULL DEFAULT 0.00,
  `gia_le` decimal(15,2) NOT NULL DEFAULT 0.00,
  `gia_si` decimal(15,2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (`id`),
  KEY `fk_ctdvt_hang_hoa` (`hang_hoa_id`),
  KEY `fk_ctdvt_dvt` (`don_vi_tinh_id`),
  CONSTRAINT `fk_ctdvt_hang_hoa` FOREIGN KEY (`hang_hoa_id`) REFERENCES `hang_hoa` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_ctdvt_dvt` FOREIGN KEY (`don_vi_tinh_id`) REFERENCES `don_vi_tinh_hang_hoa` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `chi_tiet_don_vi_tinh_cua_hang_hoa` (`id`, `hang_hoa_id`, `don_vi_tinh_id`, `cap_don_vi_tinh`, `ty_le_quy_doi`, `gia_nhap`, `gia_le`, `gia_si`) VALUES
(1, 1, 1, 1, 1.000, 5000.00, 10000.00, 8000.00),     -- Hồng cành
(2, 1, 2, 2, 20.000, 95000.00, 190000.00, 155000.00), -- Hồng bó 20 cành
(3, 2, 2, 1, 1.000, 200000.00, 350000.00, 300000.00), -- Tulip bó
(4, 3, 3, 1, 1.000, 500000.00, 950000.00, 850000.00); -- Kệ khai trương

CREATE TABLE `loai_doi_tac` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_loai_doi_tac` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_loai_doi_tac` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `loai_doi_tac` (`id`, `ma_loai_doi_tac`, `ten_loai_doi_tac`) VALUES
(1, 'KH', 'Khách hàng'),
(2, 'NCC', 'Nhà cung cấp'),
(3, 'NB', 'Khách hàng nội bộ'),
(4, 'NV', 'Nhân viên');

CREATE TABLE `nhom_doi_tac` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai_doi_tac_id` int(10) UNSIGNED NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_nhomdt_loaidt` (`loai_doi_tac_id`),
  CONSTRAINT `fk_nhomdt_loaidt` FOREIGN KEY (`loai_doi_tac_id`) REFERENCES `loai_doi_tac` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nhom_doi_tac` (`id`, `ma_nhom`, `ten_nhom`, `loai_doi_tac_id`) VALUES
(1, 'KH_VIP', 'Khách hàng VIP', 1),
(2, 'KH_LE', 'Khách mua lẻ', 1),
(3, 'NCC_DALAT', 'Nhà vườn Đà Lạt', 2),
(4, 'NV_BH', 'Nhân viên bán hàng', 4);

CREATE TABLE `doi_tac` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `loai_doi_tac_id` int(10) UNSIGNED NOT NULL,
  `nhom_doi_tac_id` int(10) UNSIGNED DEFAULT NULL,
  `ma_doi_tac` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_doi_tac` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi_chi_tiet` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phuong_xa` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quan_huyen` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `lien_he` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_tai_khoan` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ngan_hang` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ma_so_thue` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `cong_no_goi_dau` decimal(15,2) NOT NULL DEFAULT 0.00,
  `nhan_hieu` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `chiet_khau` decimal(5,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1, -- Còn hợp tác
  PRIMARY KEY (`id`),
  KEY `fk_doitac_loaidt` (`loai_doi_tac_id`),
  KEY `fk_doitac_nhom` (`nhom_doi_tac_id`),
  CONSTRAINT `fk_doitac_loaidt` FOREIGN KEY (`loai_doi_tac_id`) REFERENCES `loai_doi_tac` (`id`),
  CONSTRAINT `fk_doitac_nhom` FOREIGN KEY (`nhom_doi_tac_id`) REFERENCES `nhom_doi_tac` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `doi_tac` (`id`, `loai_doi_tac_id`, `nhom_doi_tac_id`, `ma_doi_tac`, `ten_doi_tac`, `dia_chi`, `lien_he`, `cong_no_goi_dau`, `chiet_khau`, `trang_thai`) VALUES
(1, 1, 1, 'KH001', 'Công ty Xây Dựng Miền Tây', 'Quận Ninh Kiều, Cần Thơ', '0918 123 456', 20000000.00, 5.00, 1),
(2, 1, 2, 'KH_LE', 'Khách Vãng Lai', NULL, NULL, 0.00, 0.00, 1),
(3, 2, 3, 'NCC001', 'Nhà Vườn Hoa Tươi Langbiang', 'Đà Lạt, Lâm Đồng', '0263 388 999', 50000000.00, 0.00, 1),
(4, 4, 4, 'NV001', 'Nguyễn Thị Bích Thảo', 'Cần Thơ', '0939 111 222', 0.00, 0.00, 1);

CREATE TABLE `chi_tiet_gia_doi_tac` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `doi_tac_id` int(10) UNSIGNED NOT NULL,
  `hang_hoa_id` int(10) UNSIGNED NOT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED NOT NULL,
  `chiet_khau` decimal(5,2) NOT NULL DEFAULT 0.00,
  `don_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (`id`),
  KEY `fk_giadt_doitac` (`doi_tac_id`),
  KEY `fk_giadt_hanghoa` (`hang_hoa_id`),
  KEY `fk_giadt_dvt` (`don_vi_tinh_id`),
  CONSTRAINT `fk_giadt_doitac` FOREIGN KEY (`doi_tac_id`) REFERENCES `doi_tac` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_giadt_hanghoa` FOREIGN KEY (`hang_hoa_id`) REFERENCES `hang_hoa` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_giadt_dvt` FOREIGN KEY (`don_vi_tinh_id`) REFERENCES `don_vi_tinh_hang_hoa` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chuong_trinh_khuyen_mai` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_chuong_trinh` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_chuong_trinh` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `thoi_gian_bat_dau` datetime DEFAULT NULL,
  `thoi_gian_ket_thuc` datetime DEFAULT NULL,
  `doi_tac_id` int(10) UNSIGNED DEFAULT NULL, -- NULL áp dụng cho toàn bộ khách hàng
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1, -- 1: Đang chạy, 0: Khóa dừng KM
  `mo_ta` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_ctkm_doitac` (`doi_tac_id`),
  CONSTRAINT `fk_ctkm_doitac` FOREIGN KEY (`doi_tac_id`) REFERENCES `doi_tac` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chi_tiet_khuyen_mai` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `chuong_trinh_id` int(10) UNSIGNED NOT NULL,
  `hang_hoa_id` int(10) UNSIGNED NOT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED NOT NULL,
  `so_luong_mua` decimal(15,3) NOT NULL DEFAULT 0.000,
  `hang_hoa_tang_id` int(10) UNSIGNED DEFAULT NULL,
  `don_vi_tinh_hang_tang_id` int(10) UNSIGNED DEFAULT NULL,
  `so_luong_hang_tang` decimal(15,3) DEFAULT 0.000,
  `phan_tram_giam_gia` decimal(5,2) DEFAULT 0.00,
  `so_tien_giam` decimal(15,2) DEFAULT 0.00,
  PRIMARY KEY (`id`),
  KEY `fk_ctkm_km` (`chuong_trinh_id`),
  KEY `fk_ctkm_hanghoa` (`hang_hoa_id`),
  KEY `fk_ctkm_dvt` (`don_vi_tinh_id`),
  KEY `fk_ctkm_tang_hh` (`hang_hoa_tang_id`),
  KEY `fk_ctkm_tang_dvt` (`don_vi_tinh_hang_tang_id`),
  CONSTRAINT `fk_ctkm_km` FOREIGN KEY (`chuong_trinh_id`) REFERENCES `chuong_trinh_khuyen_mai` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_ctkm_hanghoa` FOREIGN KEY (`hang_hoa_id`) REFERENCES `hang_hoa` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_ctkm_dvt` FOREIGN KEY (`don_vi_tinh_id`) REFERENCES `don_vi_tinh_hang_hoa` (`id`),
  CONSTRAINT `fk_ctkm_tang_hh` FOREIGN KEY (`hang_hoa_tang_id`) REFERENCES `hang_hoa` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_ctkm_tang_dvt` FOREIGN KEY (`don_vi_tinh_hang_tang_id`) REFERENCES `don_vi_tinh_hang_hoa` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------
-- 3. DỮ LIỆU ĐẦU KỲ & KẾT SỔ KỲ KẾ TOÁN
-- --------------------------------------------------------

CREATE TABLE `ky_ke_toan` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_ky` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_ky` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ngay_bat_dau_ky` date NOT NULL,
  `ngay_ket_thuc_ky` date NOT NULL,
  `trang_thai_khoa_so` tinyint(1) NOT NULL DEFAULT 0, -- 0: Mở sổ, 1: Khóa sổ
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `ky_ke_toan` (`id`, `ma_ky`, `ten_ky`, `ngay_bat_dau_ky`, `ngay_ket_thuc_ky`, `trang_thai_khoa_so`) VALUES
(1, 'KY_10_2026', 'Tháng 10 Năm 2026', '2026-10-01', '2026-10-31', 0);

CREATE TABLE `so_quy_dau_ky` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ky_ke_toan_id` int(10) UNSIGNED NOT NULL,
  `tien_mat` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tien_trong_tai_khoan` decimal(15,2) NOT NULL DEFAULT 0.00,
  PRIMARY KEY (`id`),
  KEY `fk_soquy_ky` (`ky_ke_toan_id`),
  CONSTRAINT `fk_soquy_ky` FOREIGN KEY (`ky_ke_toan_id`) REFERENCES `ky_ke_toan` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `so_quy_dau_ky` (`id`, `ky_ke_toan_id`, `tien_mat`, `tien_trong_tai_khoan`) VALUES
(1, 1, 50000000.00, 150000000.00);

CREATE TABLE `chi_tiet_hang_hoa_dau_ky` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `kho_hang_id` int(10) UNSIGNED NOT NULL,
  `hang_hoa_id` int(10) UNSIGNED NOT NULL,
  `ky_ke_toan_id` int(10) UNSIGNED NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `gia_von` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `noi_dung` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_cthhdk_kho` (`kho_hang_id`),
  KEY `fk_cthhdk_hanghoa` (`hang_hoa_id`),
  KEY `fk_cthhdk_ky` (`ky_ke_toan_id`),
  CONSTRAINT `fk_cthhdk_kho` FOREIGN KEY (`kho_hang_id`) REFERENCES `kho_hang` (`id`),
  CONSTRAINT `fk_cthhdk_hanghoa` FOREIGN KEY (`hang_hoa_id`) REFERENCES `hang_hoa` (`id`),
  CONSTRAINT `fk_cthhdk_ky` FOREIGN KEY (`ky_ke_toan_id`) REFERENCES `ky_ke_toan` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chi_tiet_cong_no_dau_ky` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `doi_tac_id` int(10) UNSIGNED NOT NULL,
  `ky_ke_toan_id` int(10) UNSIGNED NOT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `han_tra` date DEFAULT NULL,
  `noi_dung` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_ctcndk_doitac` (`doi_tac_id`),
  KEY `fk_ctcndk_ky` (`ky_ke_toan_id`),
  CONSTRAINT `fk_ctcndk_doitac` FOREIGN KEY (`doi_tac_id`) REFERENCES `doi_tac` (`id`),
  CONSTRAINT `fk_ctcndk_ky` FOREIGN KEY (`ky_ke_toan_id`) REFERENCES `ky_ke_toan` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------
-- 4. PHÂN HỆ NGHIỆP VỤ NHẬP / XUẤT / BÁN LẺ
-- --------------------------------------------------------

CREATE TABLE `loai_phieu_nhap_xuat` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_loai_phieu` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_loai_phieu` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `loai_phieu_nhap_xuat` (`id`, `ma_loai_phieu`, `ten_loai_phieu`) VALUES
(1, 'NHAP_HANG', 'Nhập hàng nhà cung cấp'),
(2, 'NHAP_TRA_LAI', 'Khách hàng trả lại hàng'),
(3, 'NHAP_NOI_BO', 'Nhập điều chuyển nội bộ'),
(4, 'NHAP_DIEU_CHINH', 'Nhập điều chỉnh kiểm kê / Ứng trước'),
(5, 'XUAT_BAN_BUON', 'Xuất bán buôn / bán sỉ'),
(6, 'BAN_LE', 'Bán lẻ tại quầy (POS)'),
(7, 'XUAT_TRA_LAI', 'Xuất trả lại hàng cho NCC'),
(8, 'XUAT_NOI_BO', 'Xuất điều chuyển nội bộ'),
(9, 'XUAT_DIEU_CHINH', 'Xuất hao hụt / Hủy bỏ');

CREATE TABLE `phieu_nhap_xuat_hang_hoa` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `loai_phieu_id` int(10) UNSIGNED NOT NULL,
  `ngay_chung_tu` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `thoi_han_thanh_toan` date DEFAULT NULL,
  `don_vi_id` int(10) UNSIGNED NOT NULL,
  `kho_hang_id` int(10) UNSIGNED NOT NULL,
  `kho_hang_nhan_id` int(10) UNSIGNED DEFAULT NULL, -- Kho đích khi điều chuyển nội bộ
  `doi_tac_id` int(10) UNSIGNED DEFAULT NULL,
  `nhan_vien_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_lap_id` int(10) UNSIGNED DEFAULT NULL,
  `hinh_thuc_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'TIEN_MAT', -- TIEN_MAT, CHUYEN_KHOAN, CONG_NO
  `phan_tram_thue` decimal(5,2) NOT NULL DEFAULT 0.00,
  `tien_thue` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phan_tram_chiet_khau` decimal(5,2) NOT NULL DEFAULT 0.00,
  `tien_chiet_khau` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tong_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_toan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `con_lai` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tien_khach_dua` decimal(15,2) DEFAULT 0.00, -- Nghiệp vụ bán lẻ
  `tien_thoi_lai` decimal(15,2) DEFAULT 0.00,  -- Nghiệp vụ bán lẻ
  `noi_dung` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `fk_pnx_loaiphieu` (`loai_phieu_id`),
  KEY `fk_pnx_donvi` (`don_vi_id`),
  KEY `fk_pnx_khoxuat` (`kho_hang_id`),
  KEY `fk_pnx_khonhap` (`kho_hang_nhan_id`),
  KEY `fk_pnx_doitac` (`doi_tac_id`),
  KEY `fk_pnx_nhanvien` (`nhan_vien_id`),
  KEY `fk_pnx_nguoilap` (`nguoi_lap_id`),
  CONSTRAINT `fk_pnx_loaiphieu` FOREIGN KEY (`loai_phieu_id`) REFERENCES `loai_phieu_nhap_xuat` (`id`),
  CONSTRAINT `fk_pnx_donvi` FOREIGN KEY (`don_vi_id`) REFERENCES `don_vi` (`id`),
  CONSTRAINT `fk_pnx_khoxuat` FOREIGN KEY (`kho_hang_id`) REFERENCES `kho_hang` (`id`),
  CONSTRAINT `fk_pnx_khonhap` FOREIGN KEY (`kho_hang_nhan_id`) REFERENCES `kho_hang` (`id`),
  CONSTRAINT `fk_pnx_doitac` FOREIGN KEY (`doi_tac_id`) REFERENCES `doi_tac` (`id`),
  CONSTRAINT `fk_pnx_nhanvien` FOREIGN KEY (`nhan_vien_id`) REFERENCES `doi_tac` (`id`),
  CONSTRAINT `fk_pnx_nguoilap` FOREIGN KEY (`nguoi_lap_id`) REFERENCES `nguoi_dung` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `chi_tiet_phieu_nhap_xuat` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `phieu_id` int(10) UNSIGNED NOT NULL,
  `hang_hoa_id` int(10) UNSIGNED NOT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `don_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `chiet_khau` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `fk_ctpnx_phieu` (`phieu_id`),
  KEY `fk_ctpnx_hanghoa` (`hang_hoa_id`),
  KEY `fk_ctpnx_dvt` (`don_vi_tinh_id`),
  CONSTRAINT `fk_ctpnx_phieu` FOREIGN KEY (`phieu_id`) REFERENCES `phieu_nhap_xuat_hang_hoa` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_ctpnx_hanghoa` FOREIGN KEY (`hang_hoa_id`) REFERENCES `hang_hoa` (`id`),
  CONSTRAINT `fk_ctpnx_dvt` FOREIGN KEY (`don_vi_tinh_id`) REFERENCES `don_vi_tinh_hang_hoa` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------
-- 5. PHÂN HỆ SỔ QUỸ, THU CHI & CÔNG NỢ
-- --------------------------------------------------------

CREATE TABLE `loai_phieu_thu_chi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_loai_phieu_thu_chi` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_loai_phieu_thu_chi` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai` enum('THU','CHI') NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `loai_phieu_thu_chi` (`id`, `ma_loai_phieu_thu_chi`, `ten_loai_phieu_thu_chi`, `loai`) VALUES
(1, 'THU_BAN_HANG', 'Thu tiền bán hàng trực tiếp', 'THU'),
(2, 'THU_NO_KH', 'Thu hồi công nợ khách hàng', 'THU'),
(3, 'THU_KHAC', 'Thu khác / Tiền gửi', 'THU'),
(4, 'CHI_TRA_NCC', 'Chi trả nợ tiền hàng nhà cung cấp', 'CHI'),
(5, 'CHI_VAN_HANH', 'Chi phí thuê mặt bằng / Điện nước', 'CHI'),
(6, 'CHI_LUONG', 'Chi thanh toán lương nhân viên', 'CHI'),
(7, 'CHI_KHAC', 'Chi phí khác', 'CHI');
CREATE TABLE `phieu_thu_chi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu_thu_chi` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `loai_phieu_thu_chi_id` int(10) UNSIGNED NOT NULL,
  `phieu_nhap_xuat_hang_hoa_id` int(10) UNSIGNED DEFAULT NULL,
  `ngay_chung_tu` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `doi_tac_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `hinh_thuc` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'TIEN_MAT', -- TIEN_MAT, CHUYEN_KHOAN
  `chung_tu_lien_quan` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `noi_dung` text COLLATE utf8mb4_unicode_ci,
  `nguoi_nop_nhan` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thu_quy` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `fk_ptc_loai` (`loai_phieu_thu_chi_id`),
  KEY `fk_ptc_pnx` (`phieu_nhap_xuat_hang_hoa_id`),
  KEY `fk_ptc_doitac` (`doi_tac_id`),
  CONSTRAINT `fk_ptc_loai` FOREIGN KEY (`loai_phieu_thu_chi_id`) REFERENCES `loai_phieu_thu_chi` (`id`),
  CONSTRAINT `fk_ptc_pnx` FOREIGN KEY (`phieu_nhap_xuat_hang_hoa_id`) REFERENCES `phieu_nhap_xuat_hang_hoa` (`id`) ON DELETE SET NULL,
  CONSTRAINT `fk_ptc_doitac` FOREIGN KEY (`doi_tac_id`) REFERENCES `doi_tac` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------
-- 6. VIEW HỖ TRỢ BÁO CÁO NHẬP - XUẤT - TỒN & CÔNG NỢ
-- --------------------------------------------------------

-- Tồn kho quy đổi về đơn vị gốc của từng hàng hóa.
-- Quy ước: ty_le_quy_doi = số đơn vị gốc trong 1 đơn vị đang giao dịch.
-- Tồn kho theo hàng hóa và kho; dùng truy vấn con tương quan
-- để tương thích với máy chủ không hỗ trợ subquery trong FROM của VIEW.
CREATE OR REPLACE VIEW `v_ton_kho_hien_tai` AS
SELECT
    hh.id AS hang_hoa_id,
    hh.ma_hang_hoa,
    hh.ten_hang_hoa,
    kh.id AS kho_hang_id,
    kh.ten_kho_hang,
    COALESCE(dvt_goc.ten_don_vi_tinh, '') AS don_vi_tinh_goc,
    COALESCE((
        SELECT SUM(ctdk.so_luong)
        FROM chi_tiet_hang_hoa_dau_ky ctdk
        WHERE ctdk.hang_hoa_id = hh.id
          AND ctdk.kho_hang_id = kh.id
    ), 0)
    + COALESCE((
        SELECT SUM(ct.so_luong * COALESCE(dv.ty_le_quy_doi, 1))
        FROM chi_tiet_phieu_nhap_xuat ct
        JOIN phieu_nhap_xuat_hang_hoa p ON p.id = ct.phieu_id
        LEFT JOIN chi_tiet_don_vi_tinh_cua_hang_hoa dv
          ON dv.hang_hoa_id = ct.hang_hoa_id
         AND dv.don_vi_tinh_id = ct.don_vi_tinh_id
        WHERE ct.hang_hoa_id = hh.id
          AND p.kho_hang_id = kh.id
          AND p.trang_thai = 1
          AND p.loai_phieu_id IN (1, 2, 3, 4)
    ), 0)
    - COALESCE((
        SELECT SUM(ct.so_luong * COALESCE(dv.ty_le_quy_doi, 1))
        FROM chi_tiet_phieu_nhap_xuat ct
        JOIN phieu_nhap_xuat_hang_hoa p ON p.id = ct.phieu_id
        LEFT JOIN chi_tiet_don_vi_tinh_cua_hang_hoa dv
          ON dv.hang_hoa_id = ct.hang_hoa_id
         AND dv.don_vi_tinh_id = ct.don_vi_tinh_id
        WHERE ct.hang_hoa_id = hh.id
          AND p.kho_hang_id = kh.id
          AND p.trang_thai = 1
          AND p.loai_phieu_id IN (5, 6, 7, 8, 9)
    ), 0) AS ton_kho
FROM hang_hoa hh
CROSS JOIN kho_hang kh
LEFT JOIN chi_tiet_don_vi_tinh_cua_hang_hoa ctdvt_goc
  ON ctdvt_goc.hang_hoa_id = hh.id
 AND ctdvt_goc.cap_don_vi_tinh = 1
LEFT JOIN don_vi_tinh_hang_hoa dvt_goc
  ON dvt_goc.id = ctdvt_goc.don_vi_tinh_id;

CREATE OR REPLACE VIEW `v_cong_no_doi_tac` AS
SELECT
    dt.id AS doi_tac_id,
    dt.ma_doi_tac,
    dt.ten_doi_tac,
    ldt.ma_loai_doi_tac,
    CASE
      WHEN ldt.ma_loai_doi_tac = 'NCC' THEN
        COALESCE((SELECT SUM(cndk.so_tien)
                  FROM chi_tiet_cong_no_dau_ky cndk
                  WHERE cndk.doi_tac_id = dt.id), 0)
        + COALESCE((SELECT SUM(p.con_lai)
                    FROM phieu_nhap_xuat_hang_hoa p
                    WHERE p.doi_tac_id = dt.id
                      AND p.loai_phieu_id = 1 AND p.trang_thai = 1), 0)
        - COALESCE((SELECT SUM(ptc.so_tien)
                    FROM phieu_thu_chi ptc
                    JOIN loai_phieu_thu_chi lptc
                      ON lptc.id = ptc.loai_phieu_thu_chi_id
                    WHERE ptc.doi_tac_id = dt.id
                      AND lptc.ma_loai_phieu_thu_chi = 'CHI_TRA_NCC'), 0)
      ELSE
        COALESCE((SELECT SUM(cndk.so_tien)
                  FROM chi_tiet_cong_no_dau_ky cndk
                  WHERE cndk.doi_tac_id = dt.id), 0)
        + COALESCE((SELECT SUM(p.con_lai)
                    FROM phieu_nhap_xuat_hang_hoa p
                    WHERE p.doi_tac_id = dt.id
                      AND p.loai_phieu_id IN (5, 6) AND p.trang_thai = 1), 0)
        - COALESCE((SELECT SUM(ptc.so_tien)
                    FROM phieu_thu_chi ptc
                    JOIN loai_phieu_thu_chi lptc
                      ON lptc.id = ptc.loai_phieu_thu_chi_id
                    WHERE ptc.doi_tac_id = dt.id
                      AND lptc.ma_loai_phieu_thu_chi = 'THU_NO_KH'), 0)
    END AS tong_no_hien_tai
FROM doi_tac dt
JOIN loai_doi_tac ldt ON ldt.id = dt.loai_doi_tac_id;


-- ========================================================
-- CHỈ MỤC HỖ TRỢ TÌM KIẾM VÀ BÁO CÁO
-- ========================================================
CREATE INDEX `idx_hh_ten` ON `hang_hoa` (`ten_hang_hoa`);
CREATE INDEX `idx_doitac_ten` ON `doi_tac` (`ten_doi_tac`);
CREATE INDEX `idx_pnx_ngay_loai` ON `phieu_nhap_xuat_hang_hoa` (`ngay_chung_tu`, `loai_phieu_id`);
CREATE INDEX `idx_ctpnx_hanghoa` ON `chi_tiet_phieu_nhap_xuat` (`hang_hoa_id`);
CREATE INDEX `idx_ptc_ngay` ON `phieu_thu_chi` (`ngay_chung_tu`);


-- --------------------------------------------------------
-- 7. TÀI KHOẢN MẪU, PHÂN QUYỀN HỆ THỐNG & LỚP TƯƠNG THÍCH (COMPATIBILITY LAYER)
-- --------------------------------------------------------

-- 7.1. Khởi tạo tài khoản hệ thống (Mật khẩu mặc định: 123456)
INSERT INTO `nguoi_dung` (`id`, `ten_dang_nhap`, `mat_khau`, `ho_ten`, `vai_tro`, `trang_thai`) VALUES
(1, 'admin', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Quản trị viên', 'ADMIN', 1),
(2, 'thungan', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Thu ngân', 'THU_NGAN', 1),
(3, 'thukho', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Thủ kho', 'THU_KHO', 1),
(4, 'ketoan', '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'Kế toán', 'KE_TOAN', 1)
ON DUPLICATE KEY UPDATE `ho_ten` = VALUES(`ho_ten`);

-- 7.2. Khởi tạo danh mục quyền hệ thống
INSERT INTO `quyen_he_thong` (`id`, `ma_quyen`, `ten_quyen`, `nhom_chuc_nang`) VALUES
(1, 'dashboard.view', 'Xem màn hình Tổng quan', 'Tổng quan'),
(2, 'products.view', 'Xem danh sách hàng hóa', 'Hàng hóa'),
(3, 'products.create', 'Thêm mới hàng hóa', 'Hàng hóa'),
(4, 'products.edit', 'Sửa thông tin hàng hóa', 'Hàng hóa'),
(5, 'products.delete', 'Xóa hàng hóa', 'Hàng hóa'),
(6, 'products.update_price', 'Cập nhật giá bán nhanh', 'Hàng hóa'),
(7, 'products.update_cost', 'Cập nhật giá vốn', 'Hàng hóa'),
(8, 'products.adjust_stock', 'Điều chỉnh tồn kho', 'Hàng hóa'),
(9, 'products.import', 'Import hàng hóa từ Excel', 'Hàng hóa'),
(10, 'products.export', 'Xuất Excel hàng hóa', 'Hàng hóa'),
(11, 'sales.create', 'Bán hàng / Tạo đơn POS', 'Bán hàng'),
(12, 'sales.print', 'In hóa đơn bán hàng', 'Bán hàng'),
(13, 'sales.cancel', 'Hủy hóa đơn bán hàng', 'Bán hàng'),
(14, 'purchases.view', 'Xem danh sách nhập hàng', 'Nhập hàng'),
(15, 'purchases.create', 'Tạo phiếu nhập hàng', 'Nhập hàng'),
(16, 'purchases.cancel', 'Hủy phiếu nhập hàng', 'Nhập hàng'),
(17, 'customers.view', 'Xem danh sách khách hàng', 'Khách hàng'),
(18, 'customers.create', 'Thêm mới khách hàng', 'Khách hàng'),
(19, 'customers.edit', 'Sửa thông tin khách hàng', 'Khách hàng'),
(20, 'customers.delete', 'Xóa khách hàng', 'Khách hàng'),
(21, 'suppliers.view', 'Xem danh sách nhà cung cấp', 'Nhà cung cấp'),
(22, 'suppliers.create', 'Thêm mới nhà cung cấp', 'Nhà cung cấp'),
(23, 'suppliers.edit', 'Sửa thông tin nhà cung cấp', 'Nhà cung cấp'),
(24, 'suppliers.delete', 'Xóa nhà cung cấp', 'Nhà cung cấp'),
(25, 'cashbook.view', 'Xem sổ quỹ thu chi', 'Sổ quỹ'),
(26, 'cashbook.create_receipt', 'Lập phiếu thu', 'Sổ quỹ'),
(27, 'cashbook.create_payment', 'Lập phiếu chi', 'Sổ quỹ'),
(28, 'cashbook.cancel', 'Hủy phiếu sổ quỹ', 'Sổ quỹ'),
(29, 'reports.view', 'Xem các báo cáo kinh doanh', 'Báo cáo'),
(30, 'reports.export', 'Xuất Excel báo cáo', 'Báo cáo'),
(31, 'settings.view', 'Xem cài đặt thông tin tiệm', 'Cài đặt'),
(32, 'settings.edit', 'Chỉnh sửa thông tin tiệm', 'Cài đặt'),
(33, 'users.manage', 'Quản lý tài khoản và phân quyền', 'Tài khoản')
ON DUPLICATE KEY UPDATE `ten_quyen` = VALUES(`ten_quyen`);

-- 7.3. Gán quyền mặc định cho các vai trò
-- Toàn quyền cho Quản trị viên (ADMIN)
INSERT INTO `vai_tro_quyen` (`vai_tro`, `quyen_id`, `duoc_xem`, `duoc_them`, `duoc_sua`, `duoc_xoa`, `duoc_xuat`)
SELECT 'ADMIN', id, 1, 1, 1, 1, 1 FROM `quyen_he_thong`
ON DUPLICATE KEY UPDATE `duoc_xem` = 1, `duoc_them` = 1, `duoc_sua` = 1, `duoc_xoa` = 1, `duoc_xuat` = 1;

-- Quyền cho Thu ngân (THU_NGAN): Bán hàng, khách hàng, phiếu thu, xem tổng quan & báo cáo
INSERT INTO `vai_tro_quyen` (`vai_tro`, `quyen_id`, `duoc_xem`, `duoc_them`, `duoc_sua`, `duoc_xoa`, `duoc_xuat`)
SELECT 'THU_NGAN', id, 1, 
    CASE WHEN ma_quyen IN ('sales.create', 'sales.print', 'customers.create', 'cashbook.create_receipt') THEN 1 ELSE 0 END,
    CASE WHEN ma_quyen IN ('customers.edit') THEN 1 ELSE 0 END,
    0,
    CASE WHEN ma_quyen IN ('sales.print', 'reports.export') THEN 1 ELSE 0 END
FROM `quyen_he_thong`
WHERE ma_quyen IN ('dashboard.view', 'products.view', 'sales.create', 'sales.print', 'customers.view', 'customers.create', 'customers.edit', 'cashbook.view', 'cashbook.create_receipt', 'reports.view')
ON DUPLICATE KEY UPDATE `duoc_xem` = VALUES(`duoc_xem`);

-- Quyền cho Thủ kho (THU_KHO): Quản lý hàng hóa, nhập kho, nhà cung cấp
INSERT INTO `vai_tro_quyen` (`vai_tro`, `quyen_id`, `duoc_xem`, `duoc_them`, `duoc_sua`, `duoc_xoa`, `duoc_xuat`)
SELECT 'THU_KHO', id, 1, 1, 1, 0, 1
FROM `quyen_he_thong`
WHERE ma_quyen IN ('products.view', 'products.create', 'products.edit', 'products.adjust_stock', 'products.import', 'products.export', 'purchases.view', 'purchases.create', 'suppliers.view', 'suppliers.create', 'suppliers.edit')
ON DUPLICATE KEY UPDATE `duoc_xem` = VALUES(`duoc_xem`);

-- Quyền cho Kế toán (KE_TOAN): Sổ quỹ, báo cáo, công nợ, xem hóa đơn
INSERT INTO `vai_tro_quyen` (`vai_tro`, `quyen_id`, `duoc_xem`, `duoc_them`, `duoc_sua`, `duoc_xoa`, `duoc_xuat`)
SELECT 'KE_TOAN', id, 1, 1, 1, 0, 1
FROM `quyen_he_thong`
WHERE ma_quyen IN ('dashboard.view', 'cashbook.view', 'cashbook.create_receipt', 'cashbook.create_payment', 'reports.view', 'reports.export', 'customers.view', 'suppliers.view', 'sales.create', 'purchases.view')
ON DUPLICATE KEY UPDATE `duoc_xem` = VALUES(`duoc_xem`);

-- 7.4. Bảng nhật ký hoạt động người dùng (dành cho màn hình quản trị)
CREATE TABLE IF NOT EXISTS `nhat_ky_hoat_dong` (
  `id` BIGINT(20) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nguoi_dung_id` INT(10) UNSIGNED DEFAULT NULL,
  `ten_dang_nhap` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ho_ten` VARCHAR(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `vai_tro` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `hanh_dong` VARCHAR(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `doi_tuong` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `doi_tuong_id` INT(10) UNSIGNED DEFAULT NULL,
  `ma_chung_tu` VARCHAR(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `noi_dung` TEXT COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `gia_tri` DECIMAL(15,2) DEFAULT NULL,
  `dia_chi_ip` VARCHAR(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thoi_gian` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_nhat_ky_nguoi_dung` (`nguoi_dung_id`),
  KEY `idx_nhat_ky_thoi_gian` (`thoi_gian`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7.5. Bảng thông tin tiệm (đồng bộ với bảng don_vi)
CREATE TABLE IF NOT EXISTS `thong_tin_tiem` (
  `id` int(10) UNSIGNED NOT NULL,
  `ten_tiem` varchar(200) NOT NULL DEFAULT '',
  `dia_chi` varchar(300) NOT NULL DEFAULT '',
  `so_dien_thoai` varchar(30) NOT NULL DEFAULT '',
  `email` varchar(120) NOT NULL DEFAULT '',
  `ma_so_thue` varchar(30) NOT NULL DEFAULT '',
  `website` varchar(200) NOT NULL DEFAULT '',
  `duong_dan_logo` varchar(500) NOT NULL DEFAULT '',
  `loi_cam_on` varchar(500) NOT NULL DEFAULT '',
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `thong_tin_tiem` (`id`, `ten_tiem`, `dia_chi`, `so_dien_thoai`, `website`, `ma_so_thue`, `loi_cam_on`)
SELECT 1, ten_don_vi, COALESCE(dia_chi, ''), COALESCE(lien_he, ''), COALESCE(website, ''), COALESCE(ma_so_thue, ''), 'Cảm ơn quý khách!'
FROM `don_vi` WHERE id = 1
ON DUPLICATE KEY UPDATE `ten_tiem` = VALUES(`ten_tiem`);

-- 7.6. BẢNG TƯƠNG THÍCH ỨNG DỤNG & ĐỒNG BỘ HAI CHIỀU (COMPATIBILITY & SYNC LAYER)

-- 7.6.1. Chi nhánh
CREATE TABLE IF NOT EXISTS `chi_nhanh` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_chi_nhanh` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_chi_nhanh` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_dien_thoai` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phuong_xa` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quan_huyen` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `chi_nhanh` (`id`, `ma_chi_nhanh`, `ten_chi_nhanh`, `so_dien_thoai`, `dia_chi`, `tinh_thanh`, `trang_thai`)
SELECT id, ma_don_vi, ten_don_vi, lien_he, dia_chi, tinh_thanh, trang_thai
FROM `don_vi`
ON DUPLICATE KEY UPDATE `ten_chi_nhanh` = VALUES(`ten_chi_nhanh`);

-- 7.6.2. Vai trò & phân quyền người dùng
CREATE TABLE IF NOT EXISTS `vai_tro` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_vai_tro` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_vai_tro` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `mo_ta` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `vai_tro` (`id`, `ma_vai_tro`, `ten_vai_tro`) VALUES
(1, 'ADMIN', 'Quản trị viên'),
(2, 'THU_NGAN', 'Thu ngân'),
(3, 'THU_KHO', 'Thủ kho'),
(4, 'KE_TOAN', 'Kế toán'),
(5, 'NHAN_VIEN', 'Nhân viên')
ON DUPLICATE KEY UPDATE `ten_vai_tro` = VALUES(`ten_vai_tro`);

CREATE TABLE IF NOT EXISTS `nguoi_dung_vai_tro` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `nguoi_dung_id` int(10) UNSIGNED NOT NULL,
  `vai_tro_id` int(10) UNSIGNED NOT NULL,
  PRIMARY KEY (`id`),
  KEY `fk_ndvt_nd` (`nguoi_dung_id`),
  KEY `fk_ndvt_vt` (`vai_tro_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nguoi_dung_vai_tro` (`id`, `nguoi_dung_id`, `vai_tro_id`)
SELECT 
    nd.id,
    nd.id,
    CASE 
        WHEN UPPER(nd.vai_tro) IN ('ADMIN', 'QUAN_TRI') THEN 1
        WHEN UPPER(nd.vai_tro) = 'THU_NGAN' THEN 2
        WHEN UPPER(nd.vai_tro) IN ('THU_KHO', 'KHO') THEN 3
        WHEN UPPER(nd.vai_tro) = 'KE_TOAN' THEN 4
        ELSE 5
    END
FROM `nguoi_dung` nd
ON DUPLICATE KEY UPDATE `vai_tro_id` = VALUES(`vai_tro_id`);

-- 7.6.3. Đơn vị tính, Nhóm hàng, Thương hiệu
CREATE TABLE IF NOT EXISTS `don_vi_tinh` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_don_vi` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_don_vi` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `don_vi_tinh` (`id`, `ma_don_vi`, `ten_don_vi`)
SELECT id, ma_don_vi_tinh, ten_don_vi_tinh FROM `don_vi_tinh_hang_hoa`
ON DUPLICATE KEY UPDATE `ten_don_vi` = VALUES(`ten_don_vi`);

CREATE TABLE IF NOT EXISTS `nhom_san_pham` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_nhom` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nhom_cha_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`)
SELECT id, ma_nhom_hang_hoa, ten_nhom_hang_hoa, ghi_chu FROM `nhom_hang_hoa`
ON DUPLICATE KEY UPDATE `ten_nhom` = VALUES(`ten_nhom`);

CREATE TABLE IF NOT EXISTS `thuong_hieu` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_thuong_hieu` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_thuong_hieu` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `nhom_khach_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `phan_tram_giam_gia` decimal(5,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nhom_khach_hang` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`)
SELECT id, ma_nhom, ten_nhom, ghi_chu FROM `nhom_doi_tac` WHERE loai_doi_tac_id = 1
ON DUPLICATE KEY UPDATE `ten_nhom` = VALUES(`ten_nhom`);

CREATE TABLE IF NOT EXISTS `nhom_nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nhom_nha_cung_cap` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`)
SELECT id, ma_nhom, ten_nhom, ghi_chu FROM `nhom_doi_tac` WHERE loai_doi_tac_id = 2
ON DUPLICATE KEY UPDATE `ten_nhom` = VALUES(`ten_nhom`);

-- 7.6.4. Sản phẩm & Tồn kho
CREATE TABLE IF NOT EXISTS `san_pham` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ma_vach` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
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
  `loai_hang_excel` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'Hàng hóa',
  `nhom_hang_3_cap_excel` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `vi_tri_kho` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `mo_ta` text COLLATE utf8mb4_unicode_ci,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `hinh_anh` text COLLATE utf8mb4_unicode_ci,
  `co_ban_truc_tiep` tinyint(1) NOT NULL DEFAULT 1,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  `dang_kinh_doanh` tinyint(1) NOT NULL DEFAULT 1,
  `ngay_tao` datetime DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_sp_ten` (`ten_san_pham`),
  KEY `idx_sp_ma` (`ma_san_pham`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `san_pham` (`id`, `ma_san_pham`, `ma_vach`, `ten_san_pham`, `nhom_san_pham_id`, `don_vi_tinh_id`, `gia_ban`, `gia_von`, `ton_toi_thieu`, `ton_toi_da`, `ghi_chu`, `co_ban_truc_tiep`, `trang_thai`, `dang_kinh_doanh`)
SELECT 
    hh.id,
    hh.ma_hang_hoa,
    hh.barcode,
    hh.ten_hang_hoa,
    hh.nhom_hang_hoa_id,
    ctdvt.don_vi_tinh_id,
    COALESCE(ctdvt.gia_le, 0),
    COALESCE(ctdvt.gia_nhap, 0),
    hh.du_tru_min,
    hh.du_tru_max,
    hh.ghi_chu,
    hh.in_an,
    hh.trang_thai,
    hh.trang_thai
FROM `hang_hoa` hh
LEFT JOIN `chi_tiet_don_vi_tinh_cua_hang_hoa` ctdvt ON ctdvt.hang_hoa_id = hh.id AND ctdvt.cap_don_vi_tinh = 1
ON DUPLICATE KEY UPDATE `ten_san_pham` = VALUES(`ten_san_pham`), `gia_ban` = VALUES(`gia_ban`), `gia_von` = VALUES(`gia_von`);

CREATE TABLE IF NOT EXISTS `ton_kho` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `so_luong_ton` decimal(15,3) NOT NULL DEFAULT 0.000,
  `gia_von_binh_quan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_tonkho_sp_cn` (`san_pham_id`, `chi_nhanh_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `ton_kho` (`san_pham_id`, `chi_nhanh_id`, `so_luong_ton`, `gia_von_binh_quan`)
SELECT 
    hh.id,
    1,
    COALESCE(v.ton_kho, 0),
    COALESCE(ct.gia_nhap, 0)
FROM `hang_hoa` hh
LEFT JOIN `v_ton_kho_hien_tai` v ON v.hang_hoa_id = hh.id AND v.kho_hang_id = 1
LEFT JOIN `chi_tiet_don_vi_tinh_cua_hang_hoa` ct ON ct.hang_hoa_id = hh.id AND ct.cap_don_vi_tinh = 1
ON DUPLICATE KEY UPDATE `so_luong_ton` = VALUES(`so_luong_ton`);

-- 7.6.5. Khách hàng & Nhà cung cấp
CREATE TABLE IF NOT EXISTS `khach_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_khach_hang` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_khach_hang` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai_khach_hang` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CA_NHAN',
  `so_dien_thoai` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ngay_sinh` date DEFAULT NULL,
  `gioi_tinh` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `facebook` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phuong_xa` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quan_huyen` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `nhom_khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `ma_so_thue` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_nguoi_mua_hoa_don` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_cong_ty` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi_xuat_hoa_don` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT 1,
  `nguoi_tao_id` int(10) UNSIGNED DEFAULT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  KEY `idx_kh_ten` (`ten_khach_hang`),
  KEY `idx_kh_sdt` (`so_dien_thoai`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `khach_hang` (`id`, `ma_khach_hang`, `ten_khach_hang`, `so_dien_thoai`, `email`, `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `nhom_khach_hang_id`, `ma_so_thue`, `ten_cong_ty`, `dia_chi_xuat_hoa_don`, `ghi_chu`, `trang_thai`)
SELECT 
    dt.id,
    dt.ma_doi_tac,
    dt.ten_doi_tac,
    dt.lien_he,
    dt.email,
    dt.dia_chi,
    dt.phuong_xa,
    dt.quan_huyen,
    dt.tinh_thanh,
    dt.nhom_doi_tac_id,
    dt.ma_so_thue,
    dt.nhan_hieu,
    dt.dia_chi_chi_tiet,
    dt.ghi_chu,
    dt.trang_thai
FROM `doi_tac` dt
WHERE dt.loai_doi_tac_id = 1
ON DUPLICATE KEY UPDATE `ten_khach_hang` = VALUES(`ten_khach_hang`);

CREATE TABLE IF NOT EXISTS `nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nha_cung_cap` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `ten_nha_cung_cap` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_dien_thoai` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phuong_xa` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quan_huyen` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ma_so_thue` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `nguoi_lien_he` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT 1,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nha_cung_cap` (`id`, `ma_nha_cung_cap`, `ten_nha_cung_cap`, `so_dien_thoai`, `email`, `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `ma_so_thue`, `ghi_chu`, `trang_thai`)
SELECT 
    dt.id,
    dt.ma_doi_tac,
    dt.ten_doi_tac,
    dt.lien_he,
    dt.email,
    dt.dia_chi,
    dt.phuong_xa,
    dt.quan_huyen,
    dt.tinh_thanh,
    dt.ma_so_thue,
    dt.ghi_chu,
    dt.trang_thai
FROM `doi_tac` dt
WHERE dt.loai_doi_tac_id = 2
ON DUPLICATE KEY UPDATE `ten_nha_cung_cap` = VALUES(`ten_nha_cung_cap`);

-- 7.6.6. Bán hàng & Nhập hàng
CREATE TABLE IF NOT EXISTS `hoa_don_ban_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_hoa_don` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_ban_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian_ban` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thu_khac` decimal(15,2) NOT NULL DEFAULT 0.00,
  `khach_can_tra` decimal(15,2) NOT NULL DEFAULT 0.00,
  `khach_da_thanh_toan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `con_no` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tien_khach_dua` decimal(15,2) DEFAULT 0.00,
  `tien_thoi_lai` decimal(15,2) DEFAULT 0.00,
  `phuong_thuc_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'TIEN_MAT',
  `trang_thai_thanh_toan` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DA_THANH_TOAN',
  `trang_thai_hoa_don` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `idx_hdbh_ngay` (`thoi_gian_ban`),
  KEY `idx_hdbh_kh` (`khach_hang_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `chi_tiet_hoa_don_ban_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `don_vi_tinh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `don_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `gia_von` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `idx_cthdbh_hd` (`hoa_don_id`),
  KEY `idx_cthdbh_sp` (`san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `phieu_nhap_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu_nhap` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
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
  `phuong_thuc_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT 'TIEN_MAT',
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `chi_tiet_phieu_nhap_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `phieu_nhap_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `don_gia_nhap` decimal(15,2) NOT NULL DEFAULT 0.00,
  `giam_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`),
  KEY `idx_ctpnh_pn` (`phieu_nhap_id`),
  KEY `idx_ctpnh_sp` (`san_pham_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7.6.7. Sổ quỹ (Phiếu thu / chi) & Nhóm chi phí
CREATE TABLE IF NOT EXISTS `nhom_chi_phi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `nhom_chi_phi` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`) VALUES
(1, 'DIEN_NUOC', 'Điện nước', NULL, 1),
(2, 'VAN_CHUYEN', 'Vận chuyển', NULL, 1),
(3, 'BAO_BI', 'Bao bì', NULL, 1),
(4, 'HU_HAO', 'Hư hao', NULL, 1),
(5, 'LUONG', 'Lương', NULL, 1),
(6, 'KHAC', 'Chi phí khác', NULL, 1),
(7, 'THU_KHACH_HANG', 'Thu khách hàng', NULL, 1),
(8, 'CHI_NHA_CUNG_CAP', 'Chi nhà cung cấp', NULL, 1)
ON DUPLICATE KEY UPDATE `ten_nhom` = VALUES(`ten_nhom`);

CREATE TABLE IF NOT EXISTS `phieu_chi` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu_chi` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `nhom_chi_phi_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `noi_dung` text COLLATE utf8mb4_unicode_ci,
  `nguoi_nop_nhan` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `thoi_gian_chi` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  `hoa_don_ban_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `ma_hoa_don` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_pc_ngay` (`thoi_gian_chi`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7.6.8. Các bảng phụ trợ khác (Giao hàng, Trả hàng, Thanh toán)
CREATE TABLE IF NOT EXISTS `phieu_tra_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_phieu_tra` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL DEFAULT 1,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian_tra` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_tra` decimal(15,2) NOT NULL DEFAULT 0.00,
  `tien_hoan` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ly_do` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `chi_tiet_phieu_tra_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `phieu_tra_id` int(10) UNSIGNED NOT NULL,
  `chi_tiet_hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT 0.000,
  `don_gia` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `thanh_toan` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `ma_giao_dich` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `thanh_toan_nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `phieu_nhap_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT 0.00,
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `giao_hang` (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT,
  `ma_giao_hang` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL UNIQUE,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_giao` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_dien_thoai_nguoi_giao` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `nguoi_nhan` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_dien_thoai_nguoi_nhan` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi_giao_hang` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phi_giao_hang` decimal(15,2) NOT NULL DEFAULT 0.00,
  `thoi_gian_du_kien` datetime DEFAULT NULL,
  `thoi_gian_giao` datetime DEFAULT NULL,
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CHO_GIAO',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7.6.9. Triggers đồng bộ tự động sang schema ERP (hang_hoa, doi_tac, phieu_nhap_xuat_hang_hoa, phieu_thu_chi)
DELIMITER $$

DROP TRIGGER IF EXISTS `trg_sync_san_pham_to_hang_hoa`$$
CREATE TRIGGER `trg_sync_san_pham_to_hang_hoa`
AFTER INSERT ON `san_pham`
FOR EACH ROW
BEGIN
    INSERT INTO `hang_hoa` (`id`, `nhom_hang_hoa_id`, `ma_hang_hoa`, `ten_hang_hoa`, `barcode`, `du_tru_min`, `du_tru_max`, `in_an`, `trang_thai`, `ghi_chu`)
    VALUES (NEW.id, COALESCE(NEW.nhom_san_pham_id, 1), NEW.ma_san_pham, NEW.ten_san_pham, NEW.ma_vach, NEW.ton_toi_thieu, NEW.ton_toi_da, NEW.co_ban_truc_tiep, NEW.trang_thai, NEW.ghi_chu)
    ON DUPLICATE KEY UPDATE 
        `ten_hang_hoa` = VALUES(`ten_hang_hoa`),
        `nhom_hang_hoa_id` = VALUES(`nhom_hang_hoa_id`),
        `barcode` = VALUES(`barcode`),
        `du_tru_min` = VALUES(`du_tru_min`),
        `du_tru_max` = VALUES(`du_tru_max`),
        `in_an` = VALUES(`in_an`),
        `trang_thai` = VALUES(`trang_thai`),
        `ghi_chu` = VALUES(`ghi_chu`);

    INSERT INTO `chi_tiet_don_vi_tinh_cua_hang_hoa` (`hang_hoa_id`, `don_vi_tinh_id`, `cap_don_vi_tinh`, `ty_le_quy_doi`, `gia_nhap`, `gia_le`, `gia_si`)
    VALUES (NEW.id, COALESCE(NEW.don_vi_tinh_id, 1), 1, 1.000, NEW.gia_von, NEW.gia_ban, NEW.gia_ban)
    ON DUPLICATE KEY UPDATE 
        `don_vi_tinh_id` = VALUES(`don_vi_tinh_id`),
        `gia_nhap` = VALUES(`gia_nhap`),
        `gia_le` = VALUES(`gia_le`),
        `gia_si` = VALUES(`gia_si`);
END$$

DROP TRIGGER IF EXISTS `trg_sync_khach_hang_to_doi_tac`$$
CREATE TRIGGER `trg_sync_khach_hang_to_doi_tac`
AFTER INSERT ON `khach_hang`
FOR EACH ROW
BEGIN
    INSERT INTO `doi_tac` (`id`, `loai_doi_tac_id`, `nhom_doi_tac_id`, `ma_doi_tac`, `ten_doi_tac`, `lien_he`, `email`, `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `ma_so_thue`, `nhan_hieu`, `dia_chi_chi_tiet`, `ghi_chu`, `trang_thai`)
    VALUES (NEW.id, 1, NEW.nhom_khach_hang_id, NEW.ma_khach_hang, NEW.ten_khach_hang, NEW.so_dien_thoai, NEW.email, NEW.dia_chi, NEW.phuong_xa, NEW.quan_huyen, NEW.tinh_thanh, NEW.ma_so_thue, NEW.ten_cong_ty, NEW.dia_chi_xuat_hoa_don, NEW.ghi_chu, NEW.trang_thai)
    ON DUPLICATE KEY UPDATE
        `ten_doi_tac` = VALUES(`ten_doi_tac`),
        `lien_he` = VALUES(`lien_he`),
        `email` = VALUES(`email`),
        `dia_chi` = VALUES(`dia_chi`),
        `nhom_doi_tac_id` = VALUES(`nhom_doi_tac_id`),
        `trang_thai` = VALUES(`trang_thai`);
END$$

DROP TRIGGER IF EXISTS `trg_sync_nha_cung_cap_to_doi_tac`$$
CREATE TRIGGER `trg_sync_nha_cung_cap_to_doi_tac`
AFTER INSERT ON `nha_cung_cap`
FOR EACH ROW
BEGIN
    INSERT INTO `doi_tac` (`id`, `loai_doi_tac_id`, `ma_doi_tac`, `ten_doi_tac`, `lien_he`, `email`, `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `ma_so_thue`, `ghi_chu`, `trang_thai`)
    VALUES (NEW.id, 2, NEW.ma_nha_cung_cap, NEW.ten_nha_cung_cap, NEW.so_dien_thoai, NEW.email, NEW.dia_chi, NEW.phuong_xa, NEW.quan_huyen, NEW.tinh_thanh, NEW.ma_so_thue, NEW.ghi_chu, NEW.trang_thai)
    ON DUPLICATE KEY UPDATE
        `ten_doi_tac` = VALUES(`ten_doi_tac`),
        `lien_he` = VALUES(`lien_he`),
        `email` = VALUES(`email`),
        `dia_chi` = VALUES(`dia_chi`),
        `trang_thai` = VALUES(`trang_thai`);
END$$

DROP TRIGGER IF EXISTS `trg_sync_hoa_don_to_pnx`$$
CREATE TRIGGER `trg_sync_hoa_don_to_pnx`
AFTER INSERT ON `hoa_don_ban_hang`
FOR EACH ROW
BEGIN
    INSERT INTO `phieu_nhap_xuat_hang_hoa` (`id`, `ma_phieu`, `loai_phieu_id`, `don_vi_id`, `kho_hang_id`, `doi_tac_id`, `nguoi_lap_id`, `ngay_chung_tu`, `tong_tien`, `tien_chiet_khau`, `tien_thue`, `thanh_toan`, `con_lai`, `tien_khach_dua`, `tien_thoi_lai`, `hinh_thuc_thanh_toan`, `trang_thai`, `noi_dung`)
    VALUES (NEW.id, NEW.ma_hoa_don, 6, COALESCE(NEW.chi_nhanh_id, 1), 1, NEW.khach_hang_id, NEW.nguoi_ban_id, NEW.thoi_gian_ban, NEW.khach_can_tra, NEW.giam_gia, NEW.thu_khac, NEW.khach_da_thanh_toan, NEW.con_no, NEW.tien_khach_dua, NEW.tien_thoi_lai, NEW.phuong_thuc_thanh_toan, CASE WHEN NEW.trang_thai_hoa_don = 'HUY' THEN 0 ELSE 1 END, NEW.ghi_chu)
    ON DUPLICATE KEY UPDATE
        `tong_tien` = VALUES(`tong_tien`),
        `thanh_toan` = VALUES(`thanh_toan`),
        `con_lai` = VALUES(`con_lai`),
        `trang_thai` = VALUES(`trang_thai`);
END$$

DROP TRIGGER IF EXISTS `trg_sync_phieu_chi_to_ptc`$$
CREATE TRIGGER `trg_sync_phieu_chi_to_ptc`
AFTER INSERT ON `phieu_chi`
FOR EACH ROW
BEGIN
    INSERT INTO `phieu_thu_chi` (`id`, `ma_phieu_thu_chi`, `loai_phieu_thu_chi_id`, `ngay_chung_tu`, `so_tien`, `hinh_thuc`, `noi_dung`, `nguoi_nop_nhan`)
    VALUES (NEW.id, NEW.ma_phieu_chi, CASE WHEN NEW.nhom_chi_phi_id BETWEEN 1 AND 7 THEN NEW.nhom_chi_phi_id ELSE 7 END, NEW.thoi_gian_chi, ABS(NEW.so_tien), NEW.phuong_thuc, NEW.noi_dung, NEW.nguoi_nop_nhan)
    ON DUPLICATE KEY UPDATE
        `so_tien` = VALUES(`so_tien`),
        `noi_dung` = VALUES(`noi_dung`);
END$$

DELIMITER ;
