const mysql = require('mysql2/promise');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');

// Cấu hình kết nối MySQL trực tiếp vào server 24/7 của tiệm
const dbConfig = {
  host: process.env.DB_HOST || 'baokhoagold.ddns.net',
  port: parseInt(process.env.DB_PORT || '3306'),
  user: process.env.DB_USER || 'admin_Nhut',
  password: process.env.DB_PASSWORD || 'NhutHuyMinh@2025',
  database: process.env.DB_NAME || 'hoa_shop',
  waitForConnections: true,
  connectionLimit: 10,
  queueLimit: 0,
  ssl: false
};

const JWT_SECRET = process.env.JWT_SECRET || 'ShopHoa_Super_Secret_Jwt_Security_Key_2026_Advanced_Production_Length_64_Characters!';

let pool;
function getPool() {
  if (!pool) {
    pool = mysql.createPool(dbConfig);
  }
  return pool;
}

// Helper xác thực JWT token
function verifyToken(req) {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return null;
  }
  const token = authHeader.substring(7);
  try {
    return jwt.verify(token, JWT_SECRET);
  } catch (e) {
    return null;
  }
}

// Serverless Handler cho Vercel
module.exports = async (req, res) => {
  // CORS Headers
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  const url = new URL(req.url, `http://${req.headers.host}`);
  const path = url.pathname;
  const p = getPool();

  try {
    // 1. API ĐĂNG NHẬP
    if (path === '/api/auth/login' && req.method === 'POST') {
      const body = req.body || {};
      const { tenDangNhap, matKhau } = body;

      if (!tenDangNhap || !matKhau) {
        return res.status(400).json({ message: 'Vui lòng nhập tên đăng nhập và mật khẩu.' });
      }

      const sql = `
        SELECT
            nd.id AS Id,
            nd.ma_nguoi_dung AS MaNguoiDung,
            nd.ten_dang_nhap AS TenDangNhap,
            nd.mat_khau AS MatKhau,
            nd.ho_ten AS HoTen,
            nd.so_dien_thoai AS SoDienThoai,
            nd.email AS Email,
            nd.trang_thai AS TrangThai,
            GROUP_CONCAT(vt.ten_vai_tro ORDER BY vt.id SEPARATOR ', ') AS VaiTro
        FROM nguoi_dung nd
        LEFT JOIN nguoi_dung_vai_tro ndvt ON ndvt.nguoi_dung_id = nd.id
        LEFT JOIN vai_tro vt ON vt.id = ndvt.vai_tro_id
        WHERE nd.ten_dang_nhap = ?
        GROUP BY nd.id
        LIMIT 1;
      `;

      const [rows] = await p.execute(sql, [tenDangNhap.trim()]);
      if (!rows || rows.length === 0) {
        return res.status(401).json({ message: 'Tên đăng nhập hoặc mật khẩu không chính xác.' });
      }

      const user = rows[0];
      const dbPwd = user.MatKhau || '';
      let match = false;

      if (dbPwd.startsWith('$2a$') || dbPwd.startsWith('$2b$') || dbPwd.startsWith('$2y$')) {
        match = bcrypt.compareSync(matKhau, dbPwd);
      } else {
        match = (matKhau === dbPwd);
      }

      if (!match) {
        return res.status(401).json({ message: 'Tên đăng nhập hoặc mật khẩu không chính xác.' });
      }

      const token = jwt.sign(
        { id: user.Id, username: user.TenDangNhap, name: user.HoTen, role: user.VaiTro },
        JWT_SECRET,
        { expiresIn: '7d' }
      );

      return res.status(200).json({
        token,
        user: {
          id: user.Id,
          tenDangNhap: user.TenDangNhap,
          hoTen: user.HoTen,
          vaiTro: user.VaiTro || 'Quản trị viên'
        }
      });
    }

    // Xác thực token cho các API bên dưới
    const userPayload = verifyToken(req);
    if (!userPayload) {
      return res.status(401).json({ message: 'Phiên đăng nhập hết hạn hoặc không hợp lệ.' });
    }

    // 2. API TỔNG QUAN (DASHBOARD)
    if (path === '/api/dashboard/summary' && req.method === 'GET') {
      const range = url.searchParams.get('range') || 'today';
      let startDate, endDate;
      let isHourly = false;

      const now = new Date();
      const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0);
      const todayEnd = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);

      if (range === 'yesterday') {
        startDate = new Date(todayStart.getTime() - 86400000);
        endDate = new Date(todayEnd.getTime() - 86400000);
        isHourly = true;
      } else if (range === '7days') {
        startDate = new Date(todayStart.getTime() - 6 * 86400000);
        endDate = todayEnd;
      } else if (range === 'this_month') {
        startDate = new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0);
        endDate = todayEnd;
      } else { // today
        startDate = todayStart;
        endDate = todayEnd;
        isHourly = true;
      }

      const startStr = startDate.toISOString().slice(0, 19).replace('T', ' ');
      const endStr = endDate.toISOString().slice(0, 19).replace('T', ' ');

      // 1. Thống kê tổng hợp
      const summarySql = `
        SELECT
            COALESCE(SUM(khach_can_tra), 0) AS TongDoanhThu,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(khach_da_thanh_toan), 0) AS DaThu,
            COALESCE(SUM(con_no), 0) AS ConNo
        FROM hoa_don_ban_hang
        WHERE trang_thai_hoa_don = 'HOAN_THANH'
          AND thoi_gian_ban >= ? AND thoi_gian_ban <= ?;
      `;
      const [summaryRows] = await p.execute(summarySql, [startStr, endStr]);
      const summary = summaryRows[0] || {};

      // 2. Biểu đồ
      let chartData = [];
      if (isHourly) {
        const hourlySql = `
          SELECT
              HOUR(thoi_gian_ban) AS LabelHour,
              COALESCE(SUM(khach_can_tra), 0) AS DoanhThu,
              COUNT(*) AS SoDon
          FROM hoa_don_ban_hang
          WHERE trang_thai_hoa_don = 'HOAN_THANH'
            AND thoi_gian_ban >= ? AND thoi_gian_ban <= ?
          GROUP BY HOUR(thoi_gian_ban)
          ORDER BY HOUR(thoi_gian_ban);
        `;
        const [hourRows] = await p.execute(hourlySql, [startStr, endStr]);
        const dict = {};
        hourRows.forEach(r => dict[r.LabelHour] = r);

        for (let h = 6; h <= 22; h++) {
          if (dict[h]) {
            chartData.push({ label: `${h}h`, doanhThu: Number(dict[h].DoanhThu), soDon: Number(dict[h].SoDon) });
          } else {
            chartData.push({ label: `${h}h`, doanhThu: 0, soDon: 0 });
          }
        }
      } else {
        const dailySql = `
          SELECT
              DATE_FORMAT(thoi_gian_ban, '%d/%m') AS Label,
              COALESCE(SUM(khach_can_tra), 0) AS DoanhThu,
              COUNT(*) AS SoDon
          FROM hoa_don_ban_hang
          WHERE trang_thai_hoa_don = 'HOAN_THANH'
            AND thoi_gian_ban >= ? AND thoi_gian_ban <= ?
          GROUP BY DATE(thoi_gian_ban), DATE_FORMAT(thoi_gian_ban, '%d/%m')
          ORDER BY DATE(thoi_gian_ban);
        `;
        const [dailyRows] = await p.execute(dailySql, [startStr, endStr]);
        chartData = dailyRows.map(r => ({ label: r.Label, doanhThu: Number(r.DoanhThu), soDon: Number(r.SoDon) }));
      }

      // 3. Top 10 sản phẩm
      const topSql = `
        SELECT
            ct.ten_san_pham AS tenSanPham,
            COALESCE(SUM(ct.so_luong), 0) AS soLuong,
            COALESCE(SUM(ct.thanh_tien), 0) AS doanhThu
        FROM chi_tiet_hoa_don_ban_hang ct
        INNER JOIN hoa_don_ban_hang hd ON hd.id = ct.hoa_don_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= ? AND hd.thoi_gian_ban <= ?
        GROUP BY ct.ten_san_pham
        ORDER BY doanhThu DESC
        LIMIT 10;
      `;
      const [topRows] = await p.execute(topSql, [startStr, endStr]);

      // 4. Đơn hàng gần đây
      const recentSql = `
        SELECT
            hd.id,
            hd.ma_hoa_don AS maHoaDon,
            hd.thoi_gian_ban AS thoiGianBan,
            COALESCE(kh.ten_khach_hang, 'Khách lẻ') AS tenKhachHang,
            hd.khach_can_tra AS khachCanTra,
            hd.khach_da_thanh_toan AS khachDaThanhToan,
            hd.con_no AS conNo
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
        ORDER BY hd.thoi_gian_ban DESC, hd.id DESC
        LIMIT 10;
      `;
      const [recentRows] = await p.execute(recentSql);

      return res.status(200).json({
        tongDoanhThu: Number(summary.TongDoanhThu || 0),
        soHoaDon: Number(summary.SoHoaDon || 0),
        daThu: Number(summary.DaThu || 0),
        conNo: Number(summary.ConNo || 0),
        bieuDo: chartData,
        top10SanPham: topRows.map(r => ({ tenSanPham: r.tenSanPham, soLuong: Number(r.soLuong), doanhThu: Number(r.doanhThu) })),
        donHangMoiNhat: recentRows.map(r => ({
          maHoaDon: r.maHoaDon,
          thoiGianBan: r.thoiGianBan,
          tenKhachHang: r.tenKhachHang,
          khachCanTra: Number(r.khachCanTra || 0),
          conNo: Number(r.conNo || 0)
        }))
      });
    }

    // 3. API BÁO CÁO CUỐI NGÀY
    if (path === '/api/report/end-of-day' && req.method === 'GET') {
      const dateParam = url.searchParams.get('date');
      const targetDate = dateParam ? new Date(dateParam) : new Date();
      const startStr = `${targetDate.toISOString().slice(0, 10)} 00:00:00`;
      const endStr = `${targetDate.toISOString().slice(0, 10)} 23:59:59`;

      const salesSql = `
        SELECT
            COALESCE(SUM(khach_can_tra), 0) AS TongDoanhThu,
            COUNT(*) AS SoHoaDon,
            COALESCE(SUM(khach_da_thanh_toan), 0) AS DaThu,
            COALESCE(SUM(con_no), 0) AS ConNo,
            COALESCE(SUM(giam_gia), 0) AS TongGiamGia
        FROM hoa_don_ban_hang
        WHERE trang_thai_hoa_don = 'HOAN_THANH'
          AND thoi_gian_ban >= ? AND thoi_gian_ban <= ?;
      `;
      const [salesRows] = await p.execute(salesSql, [startStr, endStr]);
      const sales = salesRows[0] || {};

      const cashFlowSql = `
        SELECT
            COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PT%' AND phuong_thuc = 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ThuTienMat,
            COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PT%' AND phuong_thuc <> 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ThuChuyenKhoan,
            COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PT%' THEN so_tien ELSE 0 END), 0) AS TongThu,
            COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PC%' AND phuong_thuc = 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ChiTienMat,
            COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PC%' AND phuong_thuc <> 'TIEN_MAT' THEN so_tien ELSE 0 END), 0) AS ChiChuyenKhoan,
            COALESCE(SUM(CASE WHEN ma_phieu_chi LIKE 'PC%' THEN so_tien ELSE 0 END), 0) AS TongChi
        FROM phieu_chi
        WHERE trang_thai = 'HOAN_THANH'
          AND thoi_gian_chi >= ? AND thoi_gian_chi <= ?;
      `;
      const [flowRows] = await p.execute(cashFlowSql, [startStr, endStr]);
      const flow = flowRows[0] || {};

      const invoicesSql = `
        SELECT
            hd.id,
            hd.ma_hoa_don AS maHoaDon,
            hd.thoi_gian_ban AS thoiGianBan,
            COALESCE(kh.ten_khach_hang, 'Khách lẻ') AS tenKhachHang,
            hd.khach_can_tra AS khachCanTra,
            hd.khach_da_thanh_toan AS khachDaThanhToan,
            hd.con_no AS conNo
        FROM hoa_don_ban_hang hd
        LEFT JOIN khach_hang kh ON kh.id = hd.khach_hang_id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.thoi_gian_ban >= ? AND hd.thoi_gian_ban <= ?
        ORDER BY hd.thoi_gian_ban DESC, hd.id DESC;
      `;
      const [invRows] = await p.execute(invoicesSql, [startStr, endStr]);

      const tongThu = Number(flow.TongThu || 0);
      const tongChi = Number(flow.TongChi || 0);

      return res.status(200).json({
        tongDoanhThu: Number(sales.TongDoanhThu || 0),
        soHoaDon: Number(sales.SoHoaDon || 0),
        daThu: Number(sales.DaThu || 0),
        conNo: Number(sales.ConNo || 0),
        tongGiamGia: Number(sales.TongGiamGia || 0),
        thuTienMat: Number(flow.ThuTienMat || 0),
        thuChuyenKhoan: Number(flow.ThuChuyenKhoan || 0),
        tongThu: tongThu,
        chiTienMat: Number(flow.ChiTienMat || 0),
        chiChuyenKhoan: Number(flow.ChiChuyenKhoan || 0),
        tongChi: tongChi,
        chenhLechThuChi: tongThu - tongChi,
        danhSachHoaDon: invRows.map(r => ({
          maHoaDon: r.maHoaDon,
          tenKhachHang: r.tenKhachHang,
          khachCanTra: Number(r.khachCanTra || 0),
          conNo: Number(r.conNo || 0)
        }))
      });
    }

    // 4. API BÁO CÁO KHÁCH NỢ
    if (path === '/api/report/debtors' && req.method === 'GET') {
      const sql = `
        SELECT
            kh.id,
            kh.ma_khach_hang AS maKhachHang,
            kh.ten_khach_hang AS tenKhachHang,
            kh.so_dien_thoai AS soDienThoai,
            COALESCE(SUM(hd.con_no), 0) AS tongNo,
            COUNT(hd.id) AS soDonChuaThanhToan
        FROM khach_hang kh
        INNER JOIN hoa_don_ban_hang hd ON hd.khach_hang_id = kh.id
        WHERE hd.trang_thai_hoa_don = 'HOAN_THANH'
          AND hd.con_no > 0
          AND kh.trang_thai = 1
        GROUP BY kh.id, kh.ma_khach_hang, kh.ten_khach_hang, kh.so_dien_thoai
        ORDER BY tongNo DESC
        LIMIT 50;
      `;
      const [rows] = await p.execute(sql);
      const totalDebt = rows.reduce((acc, cur) => acc + Number(cur.tongNo || 0), 0);

      return res.status(200).json({
        tongNoToanBo: totalDebt,
        soKhachNo: rows.length,
        danhSach: rows.map(r => ({
          tenKhachHang: r.tenKhachHang,
          soDienThoai: r.soDienThoai,
          tongNo: Number(r.tongNo || 0),
          soDonChuaThanhToan: Number(r.soDonChuaThanhToan || 0)
        }))
      });
    }

    // 5. API BÁO CÁO SỔ QUỸ
    if (path === '/api/report/cash-book' && req.method === 'GET') {
      const now = new Date();
      const start = new Date(now.getTime() - 7 * 86400000);
      const startStr = start.toISOString().slice(0, 19).replace('T', ' ');

      const sql = `
        SELECT
            id,
            ma_phieu_chi AS maPhieu,
            thoi_gian_chi AS thoiGian,
            so_tien AS soTien,
            CASE WHEN ma_phieu_chi LIKE 'PT%' THEN 'THU' ELSE 'CHI' END AS loai,
            nguoi_nhan_tien AS nguoiNhanNop,
            ly_do_chi AS ghiChu
        FROM phieu_chi
        WHERE trang_thai = 'HOAN_THANH'
          AND thoi_gian_chi >= ?
        ORDER BY thoi_gian_chi DESC
        LIMIT 50;
      `;
      const [rows] = await p.execute(sql, [startStr]);
      let diff = 0;
      rows.forEach(r => {
        if (r.loai === 'THU') diff += Number(r.soTien || 0);
        else diff -= Number(r.soTien || 0);
      });

      return res.status(200).json({
        chenhLech: diff,
        giaoDich: rows.map(r => ({
          maPhieu: r.maPhieu,
          thoiGian: r.thoiGian,
          soTien: Number(r.soTien || 0),
          loai: r.loai,
          nguoiNhanNop: r.nguoiNhanNop,
          ghiChu: r.ghiChu
        }))
      });
    }

    return res.status(404).json({ message: 'API Not Found' });
  } catch (error) {
    console.error('API Error:', error);
    return res.status(500).json({ message: 'Lỗi máy chủ kết nối cơ sở dữ liệu: ' + error.message });
  }
};
