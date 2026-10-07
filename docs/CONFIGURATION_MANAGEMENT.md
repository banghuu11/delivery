# 📋 KẾ HOẠCH QUẢN LÝ CẤU HÌNH PHẦN MỀM (SCM PLAN)
**Dự Án:** Hệ Thống Quản Lý Vận Chuyển Hỏa Tốc - TỐC ĐỘ DELIVERY  
**Công Nghệ:** ASP.NET Core 8 MVC | Entity Framework Core | SQL Server | Docker | GitHub Actions  

---

## 1. Mục Tiêu Quản Lý Cấu Hình
- Đảm bảo tính toàn vẹn, nhất quán của mã nguồn và tài liệu trong suốt vòng đời dự án.
- Chuẩn hóa quy trình làm việc nhóm, phân nhánh Git, commit message và review mã nguồn.
- Tự động hóa kiểm tra build & test thông qua GitHub Actions CI Pipeline.

---

## 2. Bảng Tổng Hợp Quy Định Của Nhóm

| Nội dung | Quy định chi tiết của nhóm |
| :--- | :--- |
| **Cấu trúc thư mục** | Phân tầng chuẩn MVC & Clean Architecture:<br>• `/Controllers`: Điều hướng & xử lý nghiệp vụ<br>• `/Models`: Entity Data Model & `/ViewModels`<br>• `/Data`: ApplicationDbContext, Seeders, Migrations<br>• `/Views`: Razor Views phân theo module (`Home`, `Admin`, `DeliveryOrder`, `Shared`)<br>• `/wwwroot`: Tài nguyên tĩnh (`css/`, `js/`, `images/`, `lib/`)<br>• `/.github`: Cấu hình CI/CD Workflows, PR/Issue Templates<br>• `/docs`: Tài liệu phân tích, đặc tả và thiết kế hệ thống |
| **Đặt tên file** | • **C# Classes / Controllers / Models:** `PascalCase` (VD: `AdminController.cs`, `DeliveryOrder.cs`)<br>• **Views / Partials:** `PascalCase`, layout phụ bắt đầu bằng `_` (VD: `_Layout.cshtml`, `_HeroBanner.cshtml`)<br>• **Assets tĩnh:** `kebab-case` hoặc `snake_case` (VD: `site.css`, `hero_banner_exact.jpg`)<br>• **Tài liệu:** `UPPER_SNAKE_CASE.md` hoặc `kebab-case.md` (VD: `README.md`, `CONFIGURATION_MANAGEMENT.md`) |
| **Version (Phiên bản)** | Tuân theo chuẩn **Semantic Versioning 2.0 (SemVer)**: `vX.Y.Z`<br>• **X (Major):** Bản phát hành lớn, thay đổi kiến trúc toàn diện (VD: `v1.0.0`, `v2.0.0`)<br>• **Y (Minor):** Thêm phân hệ / tính năng mới tương thích ngược (VD: `v1.1.0`, `v1.2.0`)<br>• **Z (Patch):** Vá lỗi nóng, sửa UI, fix bug (VD: `v1.0.1`, `v1.0.2`) |
| **Branch (Nhánh Git)** | Mô hình **Gitflow Workflow**:<br>• `main`: Nhánh production ổn định<br>• `develop`: Nhánh tích hợp chính của các sprint<br>• `feature/<ten-tinh-nang>`: Nhánh phát triển tính năng mới<br>• `bugfix/<ten-loi>`: Nhánh sửa lỗi kiểm thử<br>• `hotfix/<ten-loi-gap>`: Nhánh vá lỗi trực tiếp trên `main` |
| **Commit Message** | Chuẩn **Conventional Commits**: `<type>(<scope>): <mô tả ngắn>`<br>• `feat`: Thêm tính năng mới<br>• `fix`: Sửa lỗi<br>• `docs`: Cập nhật tài liệu<br>• `style`: Chỉnh sửa CSS / Giao diện<br>• `refactor`: Tái cấu trúc mã nguồn<br>• `ci`: Cấu hình workflow tự động hóa |
| **Merge / Code Review** | • **Không commit trực tiếp lên `main` và `develop`**.<br>• Phải mở **Pull Request (PR)** và vượt qua bước kiểm tra tự động của **GitHub Actions CI**.<br>• Yêu cầu tối thiểu **1 thành viên (Peer Review)** hoặc **Team Leader** phê duyệt trước khi Merge.<br>• Áp dụng **Squash and Merge** để giữ lịch sử commit sạch đẹp. |

---

## 3. Quy Trình Làm Việc Chi Tiết (Step-by-Step)

### Bước 1: Khởi tạo nhánh làm việc từ `develop`
```bash
git checkout develop
git pull origin develop
git checkout -b feature/order-management
```

### Bước 2: Viết mã nguồn & Commit theo quy tắc
```bash
git add .
git commit -m "feat(admin): implement order management CRUD operations"
```

### Bước 3: Đẩy lên remote repository
```bash
git push origin feature/order-management
```

### Bước 4: Mở Pull Request & Kiểm tra CI
1. Truy cập GitHub repository và nhấn **Compare & pull request**.
2. Điền thông tin theo template đã cấu hình sẵn trong `.github/pull_request_template.md`.
3. Hệ thống GitHub Actions sẽ tự động kích hoạt workflow `.github/workflows/ci.yml` để kiểm tra compile và build docker.
4. Người đánh giá (Reviewer) xem xét, comment và nhấn **Approve**.
5. Thực hiện **Squash and merge** vào nhánh `develop`.

---

## 4. Tự Động Hóa CI/CD
File cấu hình CI: `.github/workflows/ci.yml`
- Tự động chạy mỗi khi có sự kiện `push` hoặc `pull_request` vào `main` hoặc `develop`.
- Môi trường: `ubuntu-latest`
- Các bước:
  1. `actions/checkout@v4`
  2. `actions/setup-dotnet@v4` (.NET 8.0.x)
  3. `dotnet restore DeliveryManagement.sln`
  4. `dotnet build DeliveryManagement.sln --configuration Release --no-restore`
  5. `docker build` kiểm tra tính hợp lệ của Dockerfile.
