-- ============================================================
-- THÔNG TIN TIỆM — bảng lưu thông tin cửa hàng dùng chung cho hóa đơn
-- ============================================================
--
-- Mục đích
--   Thay thế các giá trị hard-code trong mẫu hóa đơn (tên tiệm, địa chỉ,
--   số điện thoại, logo, lời cảm ơn...) bằng dữ liệu lấy từ database.
--
-- Vì sao chỉ MỘT bản ghi
--   Bảng khoá chính là `id` và ràng buộc CHECK (id = 1) chặn ở tầng database,
--   nên không thể có bản ghi thứ hai dù người dùng bấm "Lưu" bao nhiêu lần hay
--   có ai đó chạy SQL tay. Ứng dụng cũng dùng INSERT ... ON DUPLICATE KEY UPDATE
--   nên lần lưu sau chỉ UPDATE đúng bản ghi này.
--
-- Áp dụng
--   Có thể chạy trực tiếp trên database đang dùng (an toàn, chạy lại nhiều lần).
--   Ứng dụng cũng tự tạo bảng này khi khởi động nếu chưa có (xem
--   Services/StoreInfoService.DamBaoBangAsync), nên không bắt buộc phải chạy
--   file script này.
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

INSERT IGNORE INTO `thong_tin_tiem`
  (`id`, `ten_tiem`, `dia_chi`, `so_dien_thoai`, `email`, `ma_so_thue`, `website`, `duong_dan_logo`, `loi_cam_on`)
VALUES
  (1, 'SHOP HOA', '', '', '', '', '', '', 'Cảm ơn quý khách!');