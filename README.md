# SHOP HOA - C# / Avalonia / MariaDB

Phần mềm quản lý shop hoa đa nền tảng Windows + Linux.

## Công nghệ

- C#
- .NET 10
- Avalonia UI
- MVVM CommunityToolkit
- Dapper
- MySqlConnector
- MariaDB/MySQL

## 1. Chuẩn bị

Cài .NET 10 SDK và VSCode.

Kiểm tra:

```bash
dotnet --version
```

## 2. Database

Import file `database/hoa_shop.sql` vào MariaDB/MySQL.

Database phải có tên:

```text
hoa_shop
```

## 3. Cấu hình kết nối

Mở:

```text
appsettings.json
```

Sửa:

```json
"Server": "127.0.0.1",
"Port": 3306,
"Database": "hoa_shop",
"User": "root",
"Password": ""
```

Không nên đưa mật khẩu thật lên GitHub.

## 4. Chạy

Mở thư mục `ShopHoa` bằng VSCode:

```bash
dotnet restore
dotnet run
```

## 5. Build Windows

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 6. Build Linux

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

## Cấu trúc

```text
ShopHoa/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── database/
├── Assets/
├── App.axaml
├── Program.cs
├── appsettings.json
└── ShopHoa.csproj
```

## Lưu ý

Bản source này là bộ khung khởi đầu: kết nối database + đọc hàng hóa + khách hàng + hóa đơn + nhà cung cấp.

Các module tiếp theo nên phát triển thành View riêng:

- Tổng quan
- Hàng hóa
- Nhập hàng
- Bán hàng/POS
- Khách hàng
- Công nợ
- Sổ quỹ
- Báo cáo
- In hóa đơn
- Barcode
- Cân điện tử
