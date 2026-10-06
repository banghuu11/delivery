# 🚚 TỐC ĐỘ DELIVERY - Hệ Thống Quản Lý Vận Chuyển Hỏa Tốc

> **Nền tảng Logistics & Quản lý giao nhận hàng hóa toàn diện** được xây dựng trên công nghệ **ASP.NET Core 8 MVC**, **Entity Framework Core (Code-First)** và **Microsoft SQL Server**, hỗ trợ đóng gói và triển khai tự động bằng **Docker**.

---

## 📌 Bảng Mục Lục
1. [Giới Thiệu Dự Án](#-giới-thiệu-dự-án)
2. [Công Nghệ Sử Dụng](#-công-nghệ-sử-dụng)
3. [Các Phân Hệ & Tính Năng](#-các-phân-hệ--tính-năng)
4. [Tài Khoản Mặc Định (Demo Accounts)](#-tài-khoản-mặc-định-demo-accounts)
5. [Hướng Dẫn Cài Đặt & Chạy Trên 2 Hệ Điều Hành](#-hướng-dẫn-cài-đặt--chạy-trên-2-hệ-điều-hành)
   - [Cách 1: Chạy bằng Docker (Khuyên dùng cho macOS & Windows)](#cách-1-chạy-bằng-docker-khuyên-dùng---dễ-nhất)
   - [Cách 2: Chạy trực tiếp trên Windows (Visual Studio / .NET CLI)](#cách-2-chạy-trực-tiếp-trên-windows-visual-studio--sql-server)
   - [Cách 3: Chạy trực tiếp trên macOS / Linux (.NET CLI)](#cách-3-chạy-trực-tiếp-trên-macos--linux)
6. [Cơ Chế Tự Động Hóa Cơ Sở Dữ Liệu (Code-First & Auto Seed)](#-cơ-chế-tự-động-hóa-cơ-sở-dữ-liệu)
7. [Cấu Trúc Thư Mục Dự Án](#-cấu-trúc-thư-mục-dự-án)

---

## 🌟 Giới Thiệu Dự Án
**TỐC ĐỘ DELIVERY** là hệ thống quản lý logistics chuyên nghiệp, giải quyết toàn bộ vòng đời của đơn hàng từ khi khách hàng tạo đơn, phân loại tiếp nhận tại bưu cục, lưu chuyển kho bãi, phân công nhân viên giao hàng (Shipper) đến khi hoàn tất giao hàng và thu hộ tiền (COD).

---

## 🛠️ Công Nghệ Sử Dụng
- **Ngôn ngữ & Framework:** C# 12, .NET 8.0, ASP.NET Core 8 MVC
- **Truy cập dữ liệu:** Entity Framework Core 8.0 (Code-First, Migrations)
- **Cơ sở dữ liệu:** Microsoft SQL Server 2022
- **Bảo mật & Phân quyền:** ASP.NET Core Identity (Role-Based Access Control - RBAC)
- **Giao diện (Frontend):** Bootstrap 5, Bootstrap Icons, Google Fonts (Plus Jakarta Sans), Custom CSS Design System, Responsive Glassmorphism
- **DevOps & Containerization:** Docker, Docker Compose, Linux Container (mcr.microsoft.com/dotnet/aspnet:8.0)

---

## 🚀 Các Phân Hệ & Tính Năng

### 1. 🌐 Khách Hàng (Customer Portal)
- **Trang chủ động (Dynamic Homepage):** Banner toàn cảnh chuẩn doanh nghiệp, thanh tính năng nổi (Dock), bảng giá cước, giới thiệu dịch vụ và quy trình giao nhận.
- **Tra cứu vận đơn (Tracking):** Theo dõi lộ trình chi tiết thời gian thực qua mã vận đơn (VD: `#TD2610...`).
- **Ước tính cước phí:** Tự động tính tiền cước dựa trên khối lượng bưu kiện, khoảng cách giao hàng (Km) và phương thức giao.
- **Tạo đơn hàng:** Form gửi đơn tiện lợi, hỗ trợ nhiều kiện hàng, chọn giao tiêu chuẩn/hỏa tốc, ghi chú hàng dễ vỡ/giá trị cao.
- **Quản lý đơn hàng cá nhân:** Xem danh sách đơn đã đặt, trạng thái xử lý và lịch sử hành trình.

### 2. 🛡️ Quản Trị Viên (Admin Portal - `/Admin`)
- **Dashboard KPI:** Thống kê tổng số đơn, doanh thu thực tế, biểu đồ phân bổ trạng thái và đơn hàng mới nhất.
- **Quản lý tài khoản & Phân quyền:** Thêm người dùng mới, đổi vai trò (Admin, Staff, Customer), khóa/mở khóa tài khoản, cấp lại mật khẩu.
- **Quản lý Đơn Hàng (Full CRUD):**
  - ➕ **Tạo mới đơn hàng:** Nhập thông tin, tạo kiện hàng, chỉ định shipper và cước phí.
  - ✏️ **Chỉnh sửa đơn hàng:** Cập nhật thông tin người nhận, địa chỉ, đổi trạng thái và shipper phụ trách.
  - 🗑️ **Xóa đơn hàng:** Modal xác nhận an toàn, tự động dọn dẹp dữ liệu ràng buộc.
  - 👁️ **Xem chi tiết & Can thiệp trạng thái:** Xem toàn bộ lịch sử trạng thái của từng đơn.
- **Quản lý danh mục loại hàng (Package Types):** Thêm, sửa, ngưng hoạt động các loại hàng bưu phẩm.
- **Quản lý bảng giá cước (Price Tables):** Cấu hình biểu phí vận chuyển theo khối lượng và khoảng cách.

### 3. 📥 Tiếp Nhận & Phân Loại (`/Reception`)
- Tiếp nhận đơn hàng mới vào bưu cục.
- Cân đo lại khối lượng thực tế và phân loại kiện hàng.

### 4. 🏬 Quản Lý Kho Bãi (`/Warehouse`)
- Check-in bưu kiện vào các kho trạm trung chuyển.
- Kiểm kê đơn hàng lưu kho và xuất kho phân phối cho Shipper.

---

## 🔑 Tài Khoản Mặc Định (Demo Accounts)

Sau khi hệ thống khởi động, dữ liệu tài khoản mẫu được tự động tạo sẵn:

| Vai trò | Email đăng nhập | Mật khẩu | Quyền hạn & Chức năng |
| :--- | :--- | :--- | :--- |
| **Quản trị viên (Admin)** | `admin@delivery.com` | `Admin@123` | Toàn quyền quản trị hệ thống, người dùng, đơn hàng, bảng giá |
| **Nhân viên Tiếp nhận** | `reception@delivery.com` | `Staff@123` | Tiếp nhận và phân loại hàng hóa |
| **Nhân viên Kho** | `warehouse@delivery.com` | `Staff@123` | Quản lý kho, nhập/xuất kiện hàng |
| **Nhân viên Giao hàng** | `shipper@delivery.com` | `Staff@123` | Giao nhận đơn hàng, cập nhật trạng thái giao |

---

## 💻 Hướng Dẫn Cài Đặt & Chạy Trên 2 Hệ Điều Hành

### Cách 1: Chạy bằng Docker (Khuyên dùng - Dễ nhất)
> Áp dụng cho cả **macOS** (Apple Silicon M1/M2/M3/M4 & Intel) và **Windows**.

#### Yêu cầu:
- Đã cài đặt **Docker Desktop** và đang bật.

#### Các bước thực hiện:
1. Mở Terminal (trên Mac) hoặc PowerShell / Command Prompt (trên Windows), chuyển đến thư mục dự án:
   ```bash
   cd DeliveryManagement
   ```
2. Khởi chạy toàn bộ hệ thống (Web App + SQL Server):
   - **Trên macOS / Linux:**
     ```bash
     ./start.sh
     # Hoặc: docker compose up --build -d
     ```
   - **Trên Windows:**
     ```cmd
     docker compose up --build -d
     ```
3. Truy cập ứng dụng:
   - **Trang chủ Website:** [http://localhost:5055](http://localhost:5055)
   - **Đăng nhập Quản trị:** [http://localhost:5055/Account/Login](http://localhost:5055/Account/Login) (Email: `admin@delivery.com` | Mật khẩu: `Admin@123`)
4. Dừng hệ thống khi không sử dụng:
   ```bash
   docker compose down
   # Hoặc trên Mac: ./stop.sh
   ```

---

### Cách 2: Chạy trực tiếp trên Windows (Visual Studio / SQL Server)

#### Yêu cầu:
- Đã cài đặt **.NET 8.0 SDK**.
- **Visual Studio 2022** (bản Community, Professional hoặc Enterprise với workload *ASP.NET and web development*).
- **Microsoft SQL Server** (bản SQL Server Express, Developer hoặc LocalDB).

#### Các bước thực hiện:
1. **Mở dự án:**
   - Mở file giải pháp `DeliveryManagement.sln` bằng **Visual Studio 2022**.
2. **Cấu hình chuỗi kết nối:**
   - Mở file [`DeliveryManagement/appsettings.json`](file:///Users/Huflit/DeliveryManagement_v1/DeliveryManagement/DeliveryManagement/appsettings.json) và chỉnh chuỗi `DefaultConnection` phù hợp với máy của bạn:
   - *Nếu dùng SQL Server cục bộ (Windows Auth):*
     ```json
     "DefaultConnection": "Server=localhost;Database=DeliveryManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     ```
   - *Nếu dùng SQL Server Express:*
     ```json
     "DefaultConnection": "Server=.\\SQLEXPRESS;Database=DeliveryManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     ```
   - *Nếu dùng LocalDB mặc định của Visual Studio:*
     ```json
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DeliveryManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true"
     ```
3. **Khởi chạy ứng dụng:**
   - Nhấn phím **F5** (hoặc `Ctrl + F5`) trên Visual Studio.
   - *Hoặc chạy bằng lệnh trên PowerShell/CMD:*
     ```cmd
     cd DeliveryManagement\DeliveryManagement
     dotnet run
     ```
4. Hệ thống sẽ tự động tạo CSDL `DeliveryManagementDb` và mở trình duyệt tại địa chỉ hiển thị trên màn hình.

---

### Cách 3: Chạy trực tiếp trên macOS / Linux

#### Yêu cầu:
- Đã cài đặt **.NET 8.0 SDK** (`brew install dotnet-sdk` trên Mac).
- Có một SQL Server instance đang chạy (có thể chạy riêng container SQL Server qua Docker: `docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 -d mcr.microsoft.com/azure-sql-edge`).

#### Các bước thực hiện:
1. Chuyển vào thư mục dự án:
   ```bash
   cd DeliveryManagement/DeliveryManagement
   ```
2. Kiểm tra chuỗi kết nối trong `appsettings.json` trỏ tới cổng 1433 của SQL Server.
3. Chạy lệnh:
   ```bash
   dotnet run
   ```
4. Mở trình duyệt theo cổng hiển thị (VD: `http://localhost:5000` hoặc `https://localhost:5001`).

---

## ⚙️ Cơ Chế Tự Động Hóa Cơ Sở Dữ Liệu

Dự án áp dụng mô hình **Entity Framework Core Code-First**:
- Mỗi khi ứng dụng khởi động ([`Program.cs`](file:///Users/Huflit/DeliveryManagement_v1/DeliveryManagement/DeliveryManagement/Program.cs#L43-L69)), hàm `dbContext.Database.MigrateAsync()` sẽ tự động kiểm tra CSDL. Nếu chưa có cơ sở dữ liệu hoặc bảng chưa đủ, hệ thống sẽ **tự động áp dụng toàn bộ Migration** mà không cần lập trình viên phải chạy lệnh SQL hay công cụ DB thủ công.
- Sau đó, hệ thống tự động kích hoạt 4 bộ nạp dữ liệu mẫu:
  1. `IdentitySeeder`: Khởi tạo các Roles (Admin, ReceptionStaff, WarehouseStaff, DeliveryStaff, Customer).
  2. `AdminSeeder`: Tạo tài khoản quản trị viên tối cao.
  3. `StaffSeeder`: Tạo các tài khoản nhân viên tiếp nhận, kho, shipper.
  4. `DataSeeder`: Nạp cấu hình thương hiệu, bảng giá, bưu cục và tính năng trang chủ.

---

## 📁 Cấu Trúc Thư Mục Dự Án

```text
DeliveryManagement/
├── DeliveryManagement.sln            # Solution chính
├── docker-compose.yml                # Cấu hình container Web & SQL Server
├── start.sh                          # Script khởi động nhanh 1 chạm (Mac/Linux)
├── stop.sh                           # Script dừng container nhanh
├── README.md                         # Tài liệu hướng dẫn dự án
└── DeliveryManagement/               # Mã nguồn ASP.NET Core MVC Web App
    ├── Controllers/                  # Điều hướng & Nghiệp vụ
    │   ├── AccountController.cs      # Xác thực, Đăng nhập, Đăng ký, Đăng xuất
    │   ├── AdminController.cs        # Quản trị viên (Dashboard, Users, Orders CRUD, PackageTypes)
    │   ├── DeliveryOrderController.cs# Nghiệp vụ khách hàng tạo & xem đơn
    │   ├── HomeController.cs         # Trang chủ, Tra cứu đơn, Báo giá
    │   ├── PriceTableController.cs   # Bảng giá cước vận chuyển
    │   ├── ReceptionController.cs    # Nghiệp vụ tiếp nhận đơn
    │   └── WarehouseController.cs    # Nghiệp vụ kiểm kê & kho bãi
    ├── Models/                       # Entity Models (Code-First)
    │   ├── ApplicationUser.cs        # Mở rộng người dùng Identity
    │   ├── DeliveryOrder.cs          # Đơn hàng vận chuyển
    │   ├── OrderItem.cs              # Chi tiết kiện hàng
    │   ├── OrderStatusHistory.cs     # Lịch sử hành trình trạng thái đơn
    │   ├── PackageType.cs            # Loại hàng hóa
    │   ├── PriceTable.cs             # Bảng giá cước
    │   ├── MapStation.cs             # Trạm bưu cục trung chuyển
    │   ├── WebsiteSetting.cs         # Cấu hình website động
    │   └── HomeFeature.cs            # Tính năng trang chủ động
    ├── Models/ViewModels/            # ViewModels truyền nhận dữ liệu
    ├── Data/                         # DbContext & Bộ nạp dữ liệu (Seeders)
    │   ├── ApplicationDbContext.cs
    │   ├── IdentitySeeder.cs
    │   ├── AdminSeeder.cs
    │   ├── StaffSeeder.cs
    │   └── DataSeeder.cs
    ├── Migrations/                   # Lịch sử EF Core Migrations
    ├── Views/                        # Razor Views giao diện
    │   ├── Home/                     # Trang chủ & các partials component
    │   ├── Admin/                    # Giao diện quản trị (Dashboard, Users, Orders, PackageTypes)
    │   ├── Account/                  # Đăng nhập, Đăng ký
    │   ├── DeliveryOrder/            # Tạo đơn & Danh sách đơn
    │   └── Shared/                   # Layout chung, Dynamic Header & Footer
    ├── wwwroot/                      # Static assets (CSS, JS, Images, Bootstrap)
    ├── Dockerfile                    # Docker build configuration
    ├── appsettings.json              # Cấu hình hệ thống & ConnectionStrings
    └── Program.cs                    # Điểm khởi đầu ứng dụng & DI Container
```

---

## 👥 Đóng Góp & Phát Triển (Git Workflow)
- **Nhánh `main`:** Mã nguồn phiên bản ổn định (Production ready).
- **Nhánh `develop`:** Nhánh phát triển tính năng mới.
- **Repository:** [https://github.com/banghuu11/delivery](https://github.com/banghuu11/delivery)

---
© 2026 **TỐC ĐỘ DELIVERY** - Hệ Thống Vận Chuyển Hỏa Tốc Thông Minh.
