-- phpMyAdmin SQL Dump
-- version 4.6.6deb5ubuntu0.5
-- https://www.phpmyadmin.net/
--
-- Máy chủ: localhost:3306
-- Thời gian đã tạo: Th8 29, 2026 lúc 08:51 PM
-- Phiên bản máy phục vụ: 10.1.48-MariaDB-0ubuntu0.18.04.1
-- Phiên bản PHP: 7.2.24-0ubuntu0.18.04.17

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Cơ sở dữ liệu: `hoa_shop`
--

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `chi_nhanh`
--

CREATE TABLE `chi_nhanh` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_chi_nhanh` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_chi_nhanh` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_dien_thoai` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phuong_xa` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quan_huyen` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1',
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `chi_nhanh`
--

INSERT INTO `chi_nhanh` (`id`, `ma_chi_nhanh`, `ten_chi_nhanh`, `so_dien_thoai`, `dia_chi`, `phuong_xa`, `quan_huyen`, `tinh_thanh`, `ghi_chu`, `trang_thai`, `ngay_tao`, `ngay_cap_nhat`) VALUES
(1, 'CN001', 'Chi nhánh trung tâm', NULL, NULL, NULL, NULL, NULL, NULL, 1, '2026-08-26 16:02:52', '2026-08-26 16:02:52');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `chi_tiet_hoa_don_ban_hang`
--

CREATE TABLE `chi_tiet_hoa_don_ban_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `don_vi_tinh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT '0.000',
  `don_gia` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `gia_von` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `chi_tiet_phieu_nhap_hang`
--

CREATE TABLE `chi_tiet_phieu_nhap_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `phieu_nhap_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT '0.000',
  `don_gia_nhap` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `chi_tiet_phieu_tra_hang`
--

CREATE TABLE `chi_tiet_phieu_tra_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `phieu_tra_id` int(10) UNSIGNED NOT NULL,
  `chi_tiet_hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT '0.000',
  `don_gia` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thanh_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `dieu_chinh_cong_no`
--

CREATE TABLE `dieu_chinh_cong_no` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_dieu_chinh` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nha_cung_cap_id` int(10) UNSIGNED DEFAULT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `loai_dieu_chinh` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ly_do` text COLLATE utf8mb4_unicode_ci,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `dieu_chinh_kho`
--

CREATE TABLE `dieu_chinh_kho` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_dieu_chinh` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `ton_cu` decimal(15,3) NOT NULL DEFAULT '0.000',
  `ton_moi` decimal(15,3) NOT NULL DEFAULT '0.000',
  `chenh_lech` decimal(15,3) NOT NULL DEFAULT '0.000',
  `ly_do` text COLLATE utf8mb4_unicode_ci,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `don_vi_tinh`
--

CREATE TABLE `don_vi_tinh` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_don_vi` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_don_vi` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `don_vi_tinh`
--

INSERT INTO `don_vi_tinh` (`id`, `ma_don_vi`, `ten_don_vi`, `ghi_chu`, `trang_thai`) VALUES
(1, 'CAI', 'Cái', NULL, 1),
(2, 'CANH', 'Cành', NULL, 1),
(3, 'BO', 'Bó', NULL, 1),
(4, 'CHAU', 'Chậu', NULL, 1),
(5, 'GIO', 'Giỏ', NULL, 1),
(6, 'KG', 'Kg', NULL, 1),
(7, 'SET', 'Bộ', NULL, 1);

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `giao_hang`
--

CREATE TABLE `giao_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_giao_hang` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `hoa_don_id` int(10) UNSIGNED NOT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_giao` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_dien_thoai_nguoi_giao` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `nguoi_nhan` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_dien_thoai_nguoi_nhan` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `dia_chi_giao_hang` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phuong_xa` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `quan_huyen` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `tinh_thanh` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phi_giao_hang` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thoi_gian_du_kien` datetime DEFAULT NULL,
  `thoi_gian_giao` datetime DEFAULT NULL,
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CHO_GIAO',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `hoa_don_ban_hang`
--

CREATE TABLE `hoa_don_ban_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_hoa_don` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_ban_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian_ban` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_hang` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thu_khac` decimal(15,2) NOT NULL DEFAULT '0.00',
  `khach_can_tra` decimal(15,2) NOT NULL DEFAULT '0.00',
  `khach_da_thanh_toan` decimal(15,2) NOT NULL DEFAULT '0.00',
  `con_no` decimal(15,2) NOT NULL DEFAULT '0.00',
  `trang_thai_thanh_toan` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DA_THANH_TOAN',
  `trang_thai_hoa_don` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `khach_hang`
--

CREATE TABLE `khach_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_khach_hang` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
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
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_tao_id` int(10) UNSIGNED DEFAULT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `lich_su_cong_no_khach_hang`
--

CREATE TABLE `lich_su_cong_no_khach_hang` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `khach_hang_id` int(10) UNSIGNED NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT NULL,
  `loai_giao_dich` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai_chung_tu` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ma_chung_tu` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_no` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ghi_co` decimal(15,2) NOT NULL DEFAULT '0.00',
  `so_du` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `lich_su_cong_no_nha_cung_cap`
--

CREATE TABLE `lich_su_cong_no_nha_cung_cap` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT NULL,
  `loai_giao_dich` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai_chung_tu` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ma_chung_tu` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_no` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ghi_co` decimal(15,2) NOT NULL DEFAULT '0.00',
  `so_du` decimal(15,2) NOT NULL DEFAULT '0.00',
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `lich_su_nhap_xuat_kho`
--

CREATE TABLE `lich_su_nhap_xuat_kho` (
  `id` bigint(20) UNSIGNED NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `loai_giao_dich` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `loai_chung_tu` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ma_chung_tu` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT '0.000',
  `ton_truoc` decimal(15,3) NOT NULL DEFAULT '0.000',
  `ton_sau` decimal(15,3) NOT NULL DEFAULT '0.000',
  `gia_von` decimal(15,2) NOT NULL DEFAULT '0.00',
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `nguoi_dung`
--

CREATE TABLE `nguoi_dung` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_nguoi_dung` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_dang_nhap` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `mat_khau` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ho_ten` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `so_dien_thoai` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT NULL,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1',
  `lan_dang_nhap_cuoi` datetime DEFAULT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `nguoi_dung`
--

INSERT INTO `nguoi_dung` (`id`, `ma_nguoi_dung`, `ten_dang_nhap`, `mat_khau`, `ho_ten`, `so_dien_thoai`, `email`, `chi_nhanh_id`, `trang_thai`, `lan_dang_nhap_cuoi`, `ngay_tao`, `ngay_cap_nhat`) VALUES
(1, 'ND000001', 'admin', 'CHANGE_ME', 'Quản trị viên', NULL, NULL, 1, 1, NULL, '2026-08-26 16:02:52', '2026-08-26 16:02:52');

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `nguoi_dung_vai_tro`
--

CREATE TABLE `nguoi_dung_vai_tro` (
  `id` int(10) UNSIGNED NOT NULL,
  `nguoi_dung_id` int(10) UNSIGNED NOT NULL,
  `vai_tro_id` int(10) UNSIGNED NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `nguoi_dung_vai_tro`
--

INSERT INTO `nguoi_dung_vai_tro` (`id`, `nguoi_dung_id`, `vai_tro_id`) VALUES
(1, 1, 1);

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `nha_cung_cap`
--

CREATE TABLE `nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_nha_cung_cap` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
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
  `chi_nhanh_id` int(10) UNSIGNED DEFAULT NULL,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `nhom_chi_phi`
--

CREATE TABLE `nhom_chi_phi` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `nhom_chi_phi`
--

INSERT INTO `nhom_chi_phi` (`id`, `ma_nhom`, `ten_nhom`, `ghi_chu`, `trang_thai`) VALUES
(1, 'DIEN_NUOC', 'Điện nước', NULL, 1),
(2, 'VAN_CHUYEN', 'Vận chuyển', NULL, 1),
(3, 'BAO_BI', 'Bao bì', NULL, 1),
(4, 'HU_HAO', 'Hư hao', NULL, 1),
(5, 'LUONG', 'Lương', NULL, 1),
(6, 'KHAC', 'Chi phí khác', NULL, 1),
(7, 'THU_KHACH_HANG', 'Thu khách hàng', NULL, 1),
(8, 'CHI_NHA_CUNG_CAP', 'Chi nhà cung cấp', NULL, 1);

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `nhom_khach_hang`
--

CREATE TABLE `nhom_khach_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `phan_tram_giam_gia` decimal(5,2) NOT NULL DEFAULT '0.00',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `nhom_khach_hang`
--

INSERT INTO `nhom_khach_hang` (`id`, `ma_nhom`, `ten_nhom`, `phan_tram_giam_gia`, `ghi_chu`, `trang_thai`) VALUES
(1, 'KHLE', 'Khách lẻ', '0.00', NULL, 1),
(2, 'KHTT', 'Khách thân thiết', '0.00', NULL, 1),
(3, 'KHSI', 'Khách sỉ', '0.00', NULL, 1),
(4, 'KHDN', 'Khách doanh nghiệp', '0.00', NULL, 1);

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `nhom_san_pham`
--

CREATE TABLE `nhom_san_pham` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_nhom` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_nhom` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nhom_cha_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `nhom_san_pham`
--

INSERT INTO `nhom_san_pham` (`id`, `ma_nhom`, `ten_nhom`, `nhom_cha_id`, `ghi_chu`, `trang_thai`) VALUES
(1, 'HOA', 'Hoa', NULL, NULL, 1),
(2, 'HOA_CAT_CANH', 'Hoa cắt cành', NULL, NULL, 1),
(3, 'HOA_CHAY', 'Hoa chậu', NULL, NULL, 1),
(4, 'HOA_NHAP', 'Hoa nhập khẩu', NULL, NULL, 1),
(5, 'PHU_KIEN', 'Phụ kiện', NULL, NULL, 1),
(6, 'GIAY_GOI', 'Giấy gói', NULL, NULL, 1),
(7, 'GIO_HOA', 'Giỏ hoa', NULL, NULL, 1);

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `phieu_chi`
--

CREATE TABLE `phieu_chi` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_phieu_chi` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `nhom_chi_phi_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `noi_dung` text COLLATE utf8mb4_unicode_ci,
  `nguoi_nop_nhan` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `thoi_gian_chi` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `phieu_nhap_hang`
--

CREATE TABLE `phieu_nhap_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_phieu_nhap` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `nha_cung_cap_id` int(10) UNSIGNED DEFAULT NULL,
  `nguoi_nhap_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian_nhap` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_hang` decimal(15,2) NOT NULL DEFAULT '0.00',
  `giam_gia` decimal(15,2) NOT NULL DEFAULT '0.00',
  `chi_phi_khac` decimal(15,2) NOT NULL DEFAULT '0.00',
  `tong_tien_nhap` decimal(15,2) NOT NULL DEFAULT '0.00',
  `da_thanh_toan` decimal(15,2) NOT NULL DEFAULT '0.00',
  `con_no` decimal(15,2) NOT NULL DEFAULT '0.00',
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DA_NHAP_HANG',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `ngay_tao` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `phieu_tra_hang`
--

CREATE TABLE `phieu_tra_hang` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_phieu_tra` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `thoi_gian_tra` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `tong_tien_tra` decimal(15,2) NOT NULL DEFAULT '0.00',
  `tien_hoan` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ly_do` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HOAN_THANH'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `san_pham`
--

CREATE TABLE `san_pham` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_san_pham` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ma_vach` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ten_san_pham` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nhom_san_pham_id` int(10) UNSIGNED DEFAULT NULL,
  `don_vi_tinh_id` int(10) UNSIGNED DEFAULT NULL,
  `thuong_hieu_id` int(10) UNSIGNED DEFAULT NULL,
  `gia_ban` decimal(15,2) NOT NULL DEFAULT '0.00',
  `gia_von` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ton_toi_thieu` decimal(15,3) NOT NULL DEFAULT '0.000',
  `ton_toi_da` decimal(15,3) NOT NULL DEFAULT '0.000',
  `vi_tri_kho` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `mo_ta` text COLLATE utf8mb4_unicode_ci,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `hinh_anh` text COLLATE utf8mb4_unicode_ci,
  `co_ban_truc_tiep` tinyint(1) NOT NULL DEFAULT '1',
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1',
  `ngay_tao` datetime DEFAULT CURRENT_TIMESTAMP,
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `thanh_phan_san_pham`
--

CREATE TABLE `thanh_phan_san_pham` (
  `id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `san_pham_thanh_phan_id` int(10) UNSIGNED NOT NULL,
  `so_luong` decimal(15,3) NOT NULL DEFAULT '1.000',
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `thanh_toan`
--

CREATE TABLE `thanh_toan` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `hoa_don_id` int(10) UNSIGNED DEFAULT NULL,
  `khach_hang_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `ma_giao_dich` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `thanh_toan_nha_cung_cap`
--

CREATE TABLE `thanh_toan_nha_cung_cap` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_thanh_toan` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nha_cung_cap_id` int(10) UNSIGNED NOT NULL,
  `phieu_nhap_id` int(10) UNSIGNED DEFAULT NULL,
  `so_tien` decimal(15,2) NOT NULL DEFAULT '0.00',
  `phuong_thuc` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TIEN_MAT',
  `thoi_gian` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `nguoi_thuc_hien_id` int(10) UNSIGNED DEFAULT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `thuong_hieu`
--

CREATE TABLE `thuong_hieu` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_thuong_hieu` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_thuong_hieu` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ghi_chu` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `ton_kho`
--

CREATE TABLE `ton_kho` (
  `id` int(10) UNSIGNED NOT NULL,
  `chi_nhanh_id` int(10) UNSIGNED NOT NULL,
  `san_pham_id` int(10) UNSIGNED NOT NULL,
  `so_luong_ton` decimal(15,3) NOT NULL DEFAULT '0.000',
  `gia_von_binh_quan` decimal(15,2) NOT NULL DEFAULT '0.00',
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Cấu trúc bảng cho bảng `vai_tro`
--

CREATE TABLE `vai_tro` (
  `id` int(10) UNSIGNED NOT NULL,
  `ma_vai_tro` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL,
  `ten_vai_tro` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `mo_ta` text COLLATE utf8mb4_unicode_ci,
  `trang_thai` tinyint(1) NOT NULL DEFAULT '1'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Đang đổ dữ liệu cho bảng `vai_tro`
--

INSERT INTO `vai_tro` (`id`, `ma_vai_tro`, `ten_vai_tro`, `mo_ta`, `trang_thai`) VALUES
(1, 'QUAN_TRI', 'Quản trị hệ thống', 'Toàn quyền', 1),
(2, 'QUAN_LY', 'Quản lý', 'Quản lý bán hàng, kho và báo cáo', 1),
(3, 'THU_NGAN', 'Thu ngân', 'Bán hàng và thanh toán', 1),
(4, 'KHO', 'Nhân viên kho', 'Nhập hàng và quản lý kho', 1),
(5, 'BAO_CAO', 'Xem báo cáo', 'Chỉ xem báo cáo', 1);

-- --------------------------------------------------------

--
-- Cấu trúc đóng vai cho view `vw_cong_no_khach_hang`
-- (See below for the actual view)
--
CREATE TABLE `vw_cong_no_khach_hang` (
`id` int(10) unsigned
,`ma_khach_hang` varchar(30)
,`ten_khach_hang` varchar(150)
,`so_dien_thoai` varchar(30)
,`con_no` decimal(38,2)
);

-- --------------------------------------------------------

--
-- Cấu trúc đóng vai cho view `vw_cong_no_nha_cung_cap`
-- (See below for the actual view)
--
CREATE TABLE `vw_cong_no_nha_cung_cap` (
`id` int(10) unsigned
,`ma_nha_cung_cap` varchar(30)
,`ten_nha_cung_cap` varchar(200)
,`so_dien_thoai` varchar(30)
,`con_no` decimal(38,2)
);

-- --------------------------------------------------------

--
-- Cấu trúc đóng vai cho view `vw_doanh_thu_ngay`
-- (See below for the actual view)
--
CREATE TABLE `vw_doanh_thu_ngay` (
`ngay` date
,`so_hoa_don` bigint(21)
,`doanh_thu` decimal(37,2)
,`da_thu` decimal(37,2)
,`con_no` decimal(37,2)
);

-- --------------------------------------------------------

--
-- Cấu trúc đóng vai cho view `vw_ton_kho`
-- (See below for the actual view)
--
CREATE TABLE `vw_ton_kho` (
`id` int(10) unsigned
,`ma_chi_nhanh` varchar(30)
,`ten_chi_nhanh` varchar(150)
,`ma_san_pham` varchar(50)
,`ten_san_pham` varchar(255)
,`ten_don_vi` varchar(100)
,`so_luong_ton` decimal(15,3)
,`gia_von_binh_quan` decimal(15,2)
,`gia_tri_ton_kho` decimal(30,5)
,`gia_ban` decimal(15,2)
,`ton_toi_thieu` decimal(15,3)
,`ton_toi_da` decimal(15,3)
);

-- --------------------------------------------------------

--
-- Cấu trúc cho view `vw_cong_no_khach_hang`
--
DROP TABLE IF EXISTS `vw_cong_no_khach_hang`;

CREATE ALGORITHM=UNDEFINED DEFINER=`admin_Nhut`@`%` SQL SECURITY DEFINER VIEW `vw_cong_no_khach_hang`  AS  select `kh`.`id` AS `id`,`kh`.`ma_khach_hang` AS `ma_khach_hang`,`kh`.`ten_khach_hang` AS `ten_khach_hang`,`kh`.`so_dien_thoai` AS `so_dien_thoai`,coalesce(sum((`ls`.`ghi_no` - `ls`.`ghi_co`)),0) AS `con_no` from (`khach_hang` `kh` left join `lich_su_cong_no_khach_hang` `ls` on((`ls`.`khach_hang_id` = `kh`.`id`))) group by `kh`.`id`,`kh`.`ma_khach_hang`,`kh`.`ten_khach_hang`,`kh`.`so_dien_thoai` ;

-- --------------------------------------------------------

--
-- Cấu trúc cho view `vw_cong_no_nha_cung_cap`
--
DROP TABLE IF EXISTS `vw_cong_no_nha_cung_cap`;

CREATE ALGORITHM=UNDEFINED DEFINER=`admin_Nhut`@`%` SQL SECURITY DEFINER VIEW `vw_cong_no_nha_cung_cap`  AS  select `ncc`.`id` AS `id`,`ncc`.`ma_nha_cung_cap` AS `ma_nha_cung_cap`,`ncc`.`ten_nha_cung_cap` AS `ten_nha_cung_cap`,`ncc`.`so_dien_thoai` AS `so_dien_thoai`,coalesce(sum((`ls`.`ghi_no` - `ls`.`ghi_co`)),0) AS `con_no` from (`nha_cung_cap` `ncc` left join `lich_su_cong_no_nha_cung_cap` `ls` on((`ls`.`nha_cung_cap_id` = `ncc`.`id`))) group by `ncc`.`id`,`ncc`.`ma_nha_cung_cap`,`ncc`.`ten_nha_cung_cap`,`ncc`.`so_dien_thoai` ;

-- --------------------------------------------------------

--
-- Cấu trúc cho view `vw_doanh_thu_ngay`
--
DROP TABLE IF EXISTS `vw_doanh_thu_ngay`;

CREATE ALGORITHM=UNDEFINED DEFINER=`admin_Nhut`@`%` SQL SECURITY DEFINER VIEW `vw_doanh_thu_ngay`  AS  select cast(`hoa_don_ban_hang`.`thoi_gian_ban` as date) AS `ngay`,count(0) AS `so_hoa_don`,sum(`hoa_don_ban_hang`.`khach_can_tra`) AS `doanh_thu`,sum(`hoa_don_ban_hang`.`khach_da_thanh_toan`) AS `da_thu`,sum(`hoa_don_ban_hang`.`con_no`) AS `con_no` from `hoa_don_ban_hang` where (`hoa_don_ban_hang`.`trang_thai_hoa_don` = 'HOAN_THANH') group by cast(`hoa_don_ban_hang`.`thoi_gian_ban` as date) ;

-- --------------------------------------------------------

--
-- Cấu trúc cho view `vw_ton_kho`
--
DROP TABLE IF EXISTS `vw_ton_kho`;

CREATE ALGORITHM=UNDEFINED DEFINER=`admin_Nhut`@`%` SQL SECURITY DEFINER VIEW `vw_ton_kho`  AS  select `tk`.`id` AS `id`,`cn`.`ma_chi_nhanh` AS `ma_chi_nhanh`,`cn`.`ten_chi_nhanh` AS `ten_chi_nhanh`,`sp`.`ma_san_pham` AS `ma_san_pham`,`sp`.`ten_san_pham` AS `ten_san_pham`,`dvt`.`ten_don_vi` AS `ten_don_vi`,`tk`.`so_luong_ton` AS `so_luong_ton`,`tk`.`gia_von_binh_quan` AS `gia_von_binh_quan`,(`tk`.`so_luong_ton` * `tk`.`gia_von_binh_quan`) AS `gia_tri_ton_kho`,`sp`.`gia_ban` AS `gia_ban`,`sp`.`ton_toi_thieu` AS `ton_toi_thieu`,`sp`.`ton_toi_da` AS `ton_toi_da` from (((`ton_kho` `tk` join `chi_nhanh` `cn` on((`cn`.`id` = `tk`.`chi_nhanh_id`))) join `san_pham` `sp` on((`sp`.`id` = `tk`.`san_pham_id`))) left join `don_vi_tinh` `dvt` on((`dvt`.`id` = `sp`.`don_vi_tinh_id`))) ;

--
-- Chỉ mục cho các bảng đã đổ
--

--
-- Chỉ mục cho bảng `chi_nhanh`
--
ALTER TABLE `chi_nhanh`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_chi_nhanh_ma` (`ma_chi_nhanh`),
  ADD KEY `idx_chi_nhanh_ten` (`ten_chi_nhanh`);

--
-- Chỉ mục cho bảng `chi_tiet_hoa_don_ban_hang`
--
ALTER TABLE `chi_tiet_hoa_don_ban_hang`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_cthdbh_hoa_don` (`hoa_don_id`),
  ADD KEY `idx_cthdbh_san_pham` (`san_pham_id`);

--
-- Chỉ mục cho bảng `chi_tiet_phieu_nhap_hang`
--
ALTER TABLE `chi_tiet_phieu_nhap_hang`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_ctpnh_phieu` (`phieu_nhap_id`),
  ADD KEY `idx_ctpnh_sp` (`san_pham_id`);

--
-- Chỉ mục cho bảng `chi_tiet_phieu_tra_hang`
--
ALTER TABLE `chi_tiet_phieu_tra_hang`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_ctpth_phieu` (`phieu_tra_id`),
  ADD KEY `fk_ctpth_chi_tiet_hd` (`chi_tiet_hoa_don_id`),
  ADD KEY `fk_ctpth_san_pham` (`san_pham_id`);

--
-- Chỉ mục cho bảng `dieu_chinh_cong_no`
--
ALTER TABLE `dieu_chinh_cong_no`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_dieu_chinh_cong_no_ma` (`ma_dieu_chinh`),
  ADD KEY `fk_dccn_khach` (`khach_hang_id`),
  ADD KEY `fk_dccn_ncc` (`nha_cung_cap_id`),
  ADD KEY `fk_dccn_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_dccn_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `dieu_chinh_kho`
--
ALTER TABLE `dieu_chinh_kho`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_dieu_chinh_ma` (`ma_dieu_chinh`),
  ADD KEY `fk_dc_kho_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_dc_kho_sp` (`san_pham_id`),
  ADD KEY `fk_dc_kho_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `don_vi_tinh`
--
ALTER TABLE `don_vi_tinh`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_dvt_ma` (`ma_don_vi`);

--
-- Chỉ mục cho bảng `giao_hang`
--
ALTER TABLE `giao_hang`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_giao_hang_ma` (`ma_giao_hang`),
  ADD KEY `idx_giao_hang_hoa_don` (`hoa_don_id`),
  ADD KEY `fk_giao_hang_khach` (`khach_hang_id`);

--
-- Chỉ mục cho bảng `hoa_don_ban_hang`
--
ALTER TABLE `hoa_don_ban_hang`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_hdbh_ma` (`ma_hoa_don`),
  ADD KEY `idx_hdbh_khach` (`khach_hang_id`),
  ADD KEY `idx_hdbh_thoi_gian` (`thoi_gian_ban`),
  ADD KEY `idx_hdbh_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_hdbh_nguoi_ban` (`nguoi_ban_id`);

--
-- Chỉ mục cho bảng `khach_hang`
--
ALTER TABLE `khach_hang`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_khach_hang_ma` (`ma_khach_hang`),
  ADD KEY `idx_khach_hang_ten` (`ten_khach_hang`),
  ADD KEY `idx_khach_hang_sdt` (`so_dien_thoai`),
  ADD KEY `idx_khach_hang_nhom` (`nhom_khach_hang_id`),
  ADD KEY `idx_khach_hang_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_khach_hang_nguoi_tao` (`nguoi_tao_id`);

--
-- Chỉ mục cho bảng `lich_su_cong_no_khach_hang`
--
ALTER TABLE `lich_su_cong_no_khach_hang`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_lsno_khach_hang` (`khach_hang_id`,`thoi_gian`),
  ADD KEY `idx_lsno_chung_tu` (`ma_chung_tu`),
  ADD KEY `fk_lsno_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_lsno_nguoi_thuc_hien` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `lich_su_cong_no_nha_cung_cap`
--
ALTER TABLE `lich_su_cong_no_nha_cung_cap`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_lsncc_ncc` (`nha_cung_cap_id`,`thoi_gian`),
  ADD KEY `fk_lsncc_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_lsncc_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `lich_su_nhap_xuat_kho`
--
ALTER TABLE `lich_su_nhap_xuat_kho`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_ls_kho_sp` (`san_pham_id`,`thoi_gian`),
  ADD KEY `idx_ls_kho_chung_tu` (`ma_chung_tu`),
  ADD KEY `fk_ls_kho_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_ls_kho_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `nguoi_dung`
--
ALTER TABLE `nguoi_dung`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_nguoi_dung_ma` (`ma_nguoi_dung`),
  ADD UNIQUE KEY `uk_nguoi_dung_ten_dang_nhap` (`ten_dang_nhap`),
  ADD KEY `idx_nguoi_dung_chi_nhanh` (`chi_nhanh_id`);

--
-- Chỉ mục cho bảng `nguoi_dung_vai_tro`
--
ALTER TABLE `nguoi_dung_vai_tro`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_nguoi_dung_vai_tro` (`nguoi_dung_id`,`vai_tro_id`),
  ADD KEY `fk_ndvt_vai_tro` (`vai_tro_id`);

--
-- Chỉ mục cho bảng `nha_cung_cap`
--
ALTER TABLE `nha_cung_cap`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_ncc_ma` (`ma_nha_cung_cap`),
  ADD KEY `idx_ncc_ten` (`ten_nha_cung_cap`(191)),
  ADD KEY `idx_ncc_sdt` (`so_dien_thoai`),
  ADD KEY `fk_ncc_chi_nhanh` (`chi_nhanh_id`);

--
-- Chỉ mục cho bảng `nhom_chi_phi`
--
ALTER TABLE `nhom_chi_phi`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_ncp_ma` (`ma_nhom`);

--
-- Chỉ mục cho bảng `nhom_khach_hang`
--
ALTER TABLE `nhom_khach_hang`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_nhom_khach_ma` (`ma_nhom`);

--
-- Chỉ mục cho bảng `nhom_san_pham`
--
ALTER TABLE `nhom_san_pham`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_nhom_sp_ma` (`ma_nhom`),
  ADD KEY `idx_nhom_sp_cha` (`nhom_cha_id`);

--
-- Chỉ mục cho bảng `phieu_chi`
--
ALTER TABLE `phieu_chi`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_phieu_chi_ma` (`ma_phieu_chi`),
  ADD KEY `idx_phieu_chi_thoi_gian` (`thoi_gian_chi`),
  ADD KEY `fk_phieu_chi_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_phieu_chi_nhom` (`nhom_chi_phi_id`),
  ADD KEY `fk_phieu_chi_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `phieu_nhap_hang`
--
ALTER TABLE `phieu_nhap_hang`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_pnh_ma` (`ma_phieu_nhap`),
  ADD KEY `idx_pnh_ncc` (`nha_cung_cap_id`),
  ADD KEY `idx_pnh_thoi_gian` (`thoi_gian_nhap`),
  ADD KEY `fk_pnh_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_pnh_nguoi` (`nguoi_nhap_id`);

--
-- Chỉ mục cho bảng `phieu_tra_hang`
--
ALTER TABLE `phieu_tra_hang`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_pth_ma` (`ma_phieu_tra`),
  ADD KEY `fk_pth_hoa_don` (`hoa_don_id`),
  ADD KEY `fk_pth_khach` (`khach_hang_id`),
  ADD KEY `fk_pth_chi_nhanh` (`chi_nhanh_id`),
  ADD KEY `fk_pth_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `san_pham`
--
ALTER TABLE `san_pham`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_san_pham_ma` (`ma_san_pham`),
  ADD UNIQUE KEY `uk_san_pham_ma_vach` (`ma_vach`),
  ADD KEY `idx_san_pham_ten` (`ten_san_pham`(191)),
  ADD KEY `idx_san_pham_nhom` (`nhom_san_pham_id`),
  ADD KEY `idx_san_pham_dvt` (`don_vi_tinh_id`),
  ADD KEY `fk_sp_thuong_hieu` (`thuong_hieu_id`);

--
-- Chỉ mục cho bảng `thanh_phan_san_pham`
--
ALTER TABLE `thanh_phan_san_pham`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_thanh_phan` (`san_pham_id`,`san_pham_thanh_phan_id`),
  ADD KEY `fk_tp_sp_thanh_phan` (`san_pham_thanh_phan_id`);

--
-- Chỉ mục cho bảng `thanh_toan`
--
ALTER TABLE `thanh_toan`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_thanh_toan_ma` (`ma_thanh_toan`),
  ADD KEY `idx_thanh_toan_hoa_don` (`hoa_don_id`),
  ADD KEY `idx_thanh_toan_khach` (`khach_hang_id`),
  ADD KEY `fk_thanh_toan_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `thanh_toan_nha_cung_cap`
--
ALTER TABLE `thanh_toan_nha_cung_cap`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_ttncc_ma` (`ma_thanh_toan`),
  ADD KEY `idx_ttncc_ncc` (`nha_cung_cap_id`,`thoi_gian`),
  ADD KEY `fk_ttncc_phieu` (`phieu_nhap_id`),
  ADD KEY `fk_ttncc_nguoi` (`nguoi_thuc_hien_id`);

--
-- Chỉ mục cho bảng `thuong_hieu`
--
ALTER TABLE `thuong_hieu`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_thuong_hieu_ma` (`ma_thuong_hieu`);

--
-- Chỉ mục cho bảng `ton_kho`
--
ALTER TABLE `ton_kho`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_ton_kho` (`chi_nhanh_id`,`san_pham_id`),
  ADD KEY `fk_ton_kho_san_pham` (`san_pham_id`);

--
-- Chỉ mục cho bảng `vai_tro`
--
ALTER TABLE `vai_tro`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uk_vai_tro_ma` (`ma_vai_tro`);

--
-- AUTO_INCREMENT cho các bảng đã đổ
--

--
-- AUTO_INCREMENT cho bảng `chi_nhanh`
--
ALTER TABLE `chi_nhanh`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;
--
-- AUTO_INCREMENT cho bảng `chi_tiet_hoa_don_ban_hang`
--
ALTER TABLE `chi_tiet_hoa_don_ban_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `chi_tiet_phieu_nhap_hang`
--
ALTER TABLE `chi_tiet_phieu_nhap_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `chi_tiet_phieu_tra_hang`
--
ALTER TABLE `chi_tiet_phieu_tra_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `dieu_chinh_cong_no`
--
ALTER TABLE `dieu_chinh_cong_no`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `dieu_chinh_kho`
--
ALTER TABLE `dieu_chinh_kho`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `don_vi_tinh`
--
ALTER TABLE `don_vi_tinh`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=8;
--
-- AUTO_INCREMENT cho bảng `giao_hang`
--
ALTER TABLE `giao_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `hoa_don_ban_hang`
--
ALTER TABLE `hoa_don_ban_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `khach_hang`
--
ALTER TABLE `khach_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `lich_su_cong_no_khach_hang`
--
ALTER TABLE `lich_su_cong_no_khach_hang`
  MODIFY `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `lich_su_cong_no_nha_cung_cap`
--
ALTER TABLE `lich_su_cong_no_nha_cung_cap`
  MODIFY `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `lich_su_nhap_xuat_kho`
--
ALTER TABLE `lich_su_nhap_xuat_kho`
  MODIFY `id` bigint(20) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `nguoi_dung`
--
ALTER TABLE `nguoi_dung`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;
--
-- AUTO_INCREMENT cho bảng `nguoi_dung_vai_tro`
--
ALTER TABLE `nguoi_dung_vai_tro`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=2;
--
-- AUTO_INCREMENT cho bảng `nha_cung_cap`
--
ALTER TABLE `nha_cung_cap`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `nhom_chi_phi`
--
ALTER TABLE `nhom_chi_phi`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=9;
--
-- AUTO_INCREMENT cho bảng `nhom_khach_hang`
--
ALTER TABLE `nhom_khach_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=5;
--
-- AUTO_INCREMENT cho bảng `nhom_san_pham`
--
ALTER TABLE `nhom_san_pham`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=8;
--
-- AUTO_INCREMENT cho bảng `phieu_chi`
--
ALTER TABLE `phieu_chi`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `phieu_nhap_hang`
--
ALTER TABLE `phieu_nhap_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `phieu_tra_hang`
--
ALTER TABLE `phieu_tra_hang`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `san_pham`
--
ALTER TABLE `san_pham`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `thanh_phan_san_pham`
--
ALTER TABLE `thanh_phan_san_pham`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `thanh_toan`
--
ALTER TABLE `thanh_toan`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `thanh_toan_nha_cung_cap`
--
ALTER TABLE `thanh_toan_nha_cung_cap`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `thuong_hieu`
--
ALTER TABLE `thuong_hieu`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `ton_kho`
--
ALTER TABLE `ton_kho`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT;
--
-- AUTO_INCREMENT cho bảng `vai_tro`
--
ALTER TABLE `vai_tro`
  MODIFY `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=6;
--
-- Các ràng buộc cho các bảng đã đổ
--

--
-- Các ràng buộc cho bảng `chi_tiet_hoa_don_ban_hang`
--
ALTER TABLE `chi_tiet_hoa_don_ban_hang`
  ADD CONSTRAINT `fk_cthdbh_hoa_don` FOREIGN KEY (`hoa_don_id`) REFERENCES `hoa_don_ban_hang` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_cthdbh_san_pham` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `chi_tiet_phieu_nhap_hang`
--
ALTER TABLE `chi_tiet_phieu_nhap_hang`
  ADD CONSTRAINT `fk_ctpnh_phieu` FOREIGN KEY (`phieu_nhap_id`) REFERENCES `phieu_nhap_hang` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ctpnh_sp` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `chi_tiet_phieu_tra_hang`
--
ALTER TABLE `chi_tiet_phieu_tra_hang`
  ADD CONSTRAINT `fk_ctpth_chi_tiet_hd` FOREIGN KEY (`chi_tiet_hoa_don_id`) REFERENCES `chi_tiet_hoa_don_ban_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ctpth_phieu` FOREIGN KEY (`phieu_tra_id`) REFERENCES `phieu_tra_hang` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ctpth_san_pham` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `dieu_chinh_cong_no`
--
ALTER TABLE `dieu_chinh_cong_no`
  ADD CONSTRAINT `fk_dccn_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_dccn_khach` FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_dccn_ncc` FOREIGN KEY (`nha_cung_cap_id`) REFERENCES `nha_cung_cap` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_dccn_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `dieu_chinh_kho`
--
ALTER TABLE `dieu_chinh_kho`
  ADD CONSTRAINT `fk_dc_kho_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_dc_kho_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_dc_kho_sp` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `giao_hang`
--
ALTER TABLE `giao_hang`
  ADD CONSTRAINT `fk_giao_hang_hoa_don` FOREIGN KEY (`hoa_don_id`) REFERENCES `hoa_don_ban_hang` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_giao_hang_khach` FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `hoa_don_ban_hang`
--
ALTER TABLE `hoa_don_ban_hang`
  ADD CONSTRAINT `fk_hdbh_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_hdbh_khach` FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_hdbh_nguoi_ban` FOREIGN KEY (`nguoi_ban_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `khach_hang`
--
ALTER TABLE `khach_hang`
  ADD CONSTRAINT `fk_khach_hang_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_khach_hang_nguoi_tao` FOREIGN KEY (`nguoi_tao_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_khach_hang_nhom` FOREIGN KEY (`nhom_khach_hang_id`) REFERENCES `nhom_khach_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `lich_su_cong_no_khach_hang`
--
ALTER TABLE `lich_su_cong_no_khach_hang`
  ADD CONSTRAINT `fk_lsno_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_lsno_khach_hang` FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_lsno_nguoi_thuc_hien` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `lich_su_cong_no_nha_cung_cap`
--
ALTER TABLE `lich_su_cong_no_nha_cung_cap`
  ADD CONSTRAINT `fk_lsncc_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_lsncc_ncc` FOREIGN KEY (`nha_cung_cap_id`) REFERENCES `nha_cung_cap` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_lsncc_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `lich_su_nhap_xuat_kho`
--
ALTER TABLE `lich_su_nhap_xuat_kho`
  ADD CONSTRAINT `fk_ls_kho_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ls_kho_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ls_kho_sp` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `nguoi_dung`
--
ALTER TABLE `nguoi_dung`
  ADD CONSTRAINT `fk_nguoi_dung_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `nguoi_dung_vai_tro`
--
ALTER TABLE `nguoi_dung_vai_tro`
  ADD CONSTRAINT `fk_ndvt_nguoi_dung` FOREIGN KEY (`nguoi_dung_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ndvt_vai_tro` FOREIGN KEY (`vai_tro_id`) REFERENCES `vai_tro` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `nha_cung_cap`
--
ALTER TABLE `nha_cung_cap`
  ADD CONSTRAINT `fk_ncc_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `nhom_san_pham`
--
ALTER TABLE `nhom_san_pham`
  ADD CONSTRAINT `fk_nhom_sp_cha` FOREIGN KEY (`nhom_cha_id`) REFERENCES `nhom_san_pham` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `phieu_chi`
--
ALTER TABLE `phieu_chi`
  ADD CONSTRAINT `fk_phieu_chi_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_phieu_chi_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_phieu_chi_nhom` FOREIGN KEY (`nhom_chi_phi_id`) REFERENCES `nhom_chi_phi` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `phieu_nhap_hang`
--
ALTER TABLE `phieu_nhap_hang`
  ADD CONSTRAINT `fk_pnh_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_pnh_ncc` FOREIGN KEY (`nha_cung_cap_id`) REFERENCES `nha_cung_cap` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_pnh_nguoi` FOREIGN KEY (`nguoi_nhap_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `phieu_tra_hang`
--
ALTER TABLE `phieu_tra_hang`
  ADD CONSTRAINT `fk_pth_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_pth_hoa_don` FOREIGN KEY (`hoa_don_id`) REFERENCES `hoa_don_ban_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_pth_khach` FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_pth_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `san_pham`
--
ALTER TABLE `san_pham`
  ADD CONSTRAINT `fk_sp_dvt` FOREIGN KEY (`don_vi_tinh_id`) REFERENCES `don_vi_tinh` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_sp_nhom` FOREIGN KEY (`nhom_san_pham_id`) REFERENCES `nhom_san_pham` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_sp_thuong_hieu` FOREIGN KEY (`thuong_hieu_id`) REFERENCES `thuong_hieu` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `thanh_phan_san_pham`
--
ALTER TABLE `thanh_phan_san_pham`
  ADD CONSTRAINT `fk_tp_sp` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_tp_sp_thanh_phan` FOREIGN KEY (`san_pham_thanh_phan_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `thanh_toan`
--
ALTER TABLE `thanh_toan`
  ADD CONSTRAINT `fk_thanh_toan_hoa_don` FOREIGN KEY (`hoa_don_id`) REFERENCES `hoa_don_ban_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_thanh_toan_khach` FOREIGN KEY (`khach_hang_id`) REFERENCES `khach_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_thanh_toan_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- Các ràng buộc cho bảng `thanh_toan_nha_cung_cap`
--
ALTER TABLE `thanh_toan_nha_cung_cap`
  ADD CONSTRAINT `fk_ttncc_ncc` FOREIGN KEY (`nha_cung_cap_id`) REFERENCES `nha_cung_cap` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ttncc_nguoi` FOREIGN KEY (`nguoi_thuc_hien_id`) REFERENCES `nguoi_dung` (`id`) ON DELETE SET NULL ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ttncc_phieu` FOREIGN KEY (`phieu_nhap_id`) REFERENCES `phieu_nhap_hang` (`id`) ON DELETE SET NULL ON UPDATE CASCADE;

--
-- PHIẾU NHẬP HÀNG - bổ sung (chạy 1 lần cho database đã có sẵn)
--
-- Hai cột này là TÙY CHỌN. Nếu database chưa có thì ứng dụng vẫn chạy bình
-- thường, chỉ bỏ qua việc lưu "số hóa đơn đầu vào" và "mã đặt hàng nhập".
-- Chạy lại đoạn dưới sau khi đã thêm thì không bị lỗi.
--
SET @co_so_dat = IFNULL((SELECT `information_schema`.`SCHEMA_NAME` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'phieu_nhap_hang' LIMIT 1), '');

SET @co_hoa_don = IFNULL((SELECT `information_schema`.`COLUMN_NAME` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'phieu_nhap_hang' AND `COLUMN_NAME` = 'so_hoa_don_dau_vao' LIMIT 1), '');

SET @co_dat_hang = IFNULL((SELECT `information_schema`.`COLUMN_NAME` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'phieu_nhap_hang' AND `COLUMN_NAME` = 'ma_dat_hang_nhap' LIMIT 1), '');

SET @sql = IF(@co_so_dat = '' OR @co_hoa_don <> '', 'SELECT 1', 'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `so_hoa_don_dau_vao` varchar(50) DEFAULT NULL AFTER `ghi_chu`');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(@co_so_dat = '' OR @co_dat_hang <> '', 'SELECT 1', 'ALTER TABLE `phieu_nhap_hang` ADD COLUMN `ma_dat_hang_nhap` varchar(50) DEFAULT NULL AFTER `so_hoa_don_dau_vao`');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

--
-- Sổ quỹ: hai cột `nguoi_nop_nhan` và `ghi_chu` cho bảng `phieu_chi`.
--
-- Trước đây bảng này chỉ có `noi_dung` (text) nên phần "người nộp / nhận" và
-- "ghi chú" phải nhồi chung vào một ô, không tách được trên danh sách, không xuất
-- được ra Excel và không nhập lại từ file. Hai cột mới tách riêng từng thông tin:
--   nguoi_nop_nhan : người nộp (phiếu thu) hoặc người nhận (phiếu chi)
--   ghi_chu        : ghi chú tự do của phiếu
-- `nguoi_thuc_hien_id` giữ đúng nghĩa FK là người tạo chứng từ.
--
-- Chạy lại đoạn dưới nhiều lần vẫn an toàn (đã có cột thì bỏ qua).
--
SET @co_so_dat_sq = IFNULL((SELECT `information_schema`.`SCHEMA_NAME` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'phieu_chi' LIMIT 1), '');

SET @co_nop_nhan = IFNULL((SELECT `information_schema`.`COLUMN_NAME` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'phieu_chi' AND `COLUMN_NAME` = 'nguoi_nop_nhan' LIMIT 1), '');

SET @co_ghi_chu_sq = IFNULL((SELECT `information_schema`.`COLUMN_NAME` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'phieu_chi' AND `COLUMN_NAME` = 'ghi_chu' LIMIT 1), '');

SET @sql = IF(@co_so_dat_sq = '' OR @co_nop_nhan <> '', 'SELECT 1', 'ALTER TABLE `phieu_chi` ADD COLUMN `nguoi_nop_nhan` varchar(150) DEFAULT NULL AFTER `noi_dung`');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(@co_so_dat_sq = '' OR @co_ghi_chu_sq <> '', 'SELECT 1', 'ALTER TABLE `phieu_chi` ADD COLUMN `ghi_chu` text DEFAULT NULL AFTER `nguoi_nop_nhan`');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

--
-- Các ràng buộc cho bảng `ton_kho`
--
ALTER TABLE `ton_kho`
  ADD CONSTRAINT `fk_ton_kho_chi_nhanh` FOREIGN KEY (`chi_nhanh_id`) REFERENCES `chi_nhanh` (`id`) ON UPDATE CASCADE,
  ADD CONSTRAINT `fk_ton_kho_san_pham` FOREIGN KEY (`san_pham_id`) REFERENCES `san_pham` (`id`) ON UPDATE CASCADE;

--
-- Bảng thông tin tiệm: chỉ MỘT bản ghi (id = 1)
--
-- Hóa đơn / phiếu nhập / báo cáo đọc thông tin này thay cho giá trị hard-code.
-- Ràng buộc CHECK (id = 1) chặn ở tầng database, nên kể cả khi có ai đó chạy
-- SQL tay cũng không thể tạo thêm bản ghi thứ hai làm sai thông tin in ra.
--
CREATE TABLE IF NOT EXISTS `thong_tin_tiem` (
  `id` int(10) UNSIGNED NOT NULL,
  `ten_tiem` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `dia_chi` varchar(300) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `so_dien_thoai` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `email` varchar(120) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `ma_so_thue` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `website` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `duong_dan_logo` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `loi_cam_on` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '',
  `ngay_cap_nhat` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  CONSTRAINT `ck_thong_tin_tiem_id` CHECK (`id` = 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- Bản ghi thông tin tiệm đầu tiên. INSERT IGNORE nên chạy lại script này
-- nhiều lần vẫn chỉ có một dòng.
--
INSERT IGNORE INTO `thong_tin_tiem`
  (`id`, `ten_tiem`, `dia_chi`, `so_dien_thoai`, `email`, `ma_so_thue`, `website`, `duong_dan_logo`, `loi_cam_on`)
VALUES
  (1, 'SHOP HOA', '', '', '', '', '', '', 'Cảm ơn quý khách!');

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
