INSERT INTO PackageTypes (TypeName, Description, IsActive)
VALUES
(N'Gói nhỏ', N'Hàng hóa có kích thước nhỏ', 1),
(N'Gói bọc', N'Hàng hóa được bọc bên ngoài', 1),
(N'Bao', N'Hàng hóa đóng trong bao', 1),
(N'Thùng', N'Hàng hóa đóng trong thùng', 1),
(N'Tivi', N'Tivi và thiết bị màn hình', 1),
(N'Laptop', N'Máy tính xách tay', 1),
(N'Máy tính', N'Máy tính để bàn', 1),
(N'CPU', N'Bộ xử lý máy tính', 1),
(N'Xe', N'Xe hoặc phương tiện cần vận chuyển', 1);
SELECT *
FROM PackageTypes;