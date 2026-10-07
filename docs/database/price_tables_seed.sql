-- Dữ liệu mẫu bảng giá (tùy chọn). Cũng có thể nhập qua trang Admin > Bảng giá.
INSERT INTO PriceTables (DeliveryMethod, MinWeight, MaxWeight, MinDistance, MaxDistance, WeightRange, DistanceRange, Price)
VALUES
(N'Thường', 0, 5, 0, 5, N'0 - 5 kg', N'0 - 5 km', 20000),
(N'Thường', 0, 5, 5, 10, N'0 - 5 kg', N'5 - 10 km', 25000),
(N'Thường', 0, 5, 10, 20, N'0 - 5 kg', N'10 - 20 km', 30000),
(N'Thường', 0, 5, 20, 50, N'0 - 5 kg', N'20 - 50 km', 40000),
(N'Thường', 5, 20, 0, 5, N'5 - 20 kg', N'0 - 5 km', 40000),
(N'Thường', 5, 20, 5, 10, N'5 - 20 kg', N'5 - 10 km', 45000),
(N'Thường', 5, 20, 10, 20, N'5 - 20 kg', N'10 - 20 km', 55000),
(N'Thường', 5, 20, 20, 50, N'5 - 20 kg', N'20 - 50 km', 70000),
(N'Thường', 20, 100, 0, 5, N'20 - 100 kg', N'0 - 5 km', 80000),
(N'Thường', 20, 100, 5, 10, N'20 - 100 kg', N'5 - 10 km', 95000),
(N'Thường', 20, 100, 10, 20, N'20 - 100 kg', N'10 - 20 km', 110000),
(N'Thường', 20, 100, 20, 50, N'20 - 100 kg', N'20 - 50 km', 130000),
(N'Nhanh', 0, 5, 0, 5, N'0 - 5 kg', N'0 - 5 km', 35000),
(N'Nhanh', 0, 5, 5, 10, N'0 - 5 kg', N'5 - 10 km', 40000),
(N'Nhanh', 0, 5, 10, 20, N'0 - 5 kg', N'10 - 20 km', 50000),
(N'Nhanh', 0, 5, 20, 50, N'0 - 5 kg', N'20 - 50 km', 65000),
(N'Nhanh', 5, 20, 0, 5, N'5 - 20 kg', N'0 - 5 km', 70000),
(N'Nhanh', 5, 20, 5, 10, N'5 - 20 kg', N'5 - 10 km', 80000),
(N'Nhanh', 5, 20, 10, 20, N'5 - 20 kg', N'10 - 20 km', 95000),
(N'Nhanh', 5, 20, 20, 50, N'5 - 20 kg', N'20 - 50 km', 115000),
(N'Nhanh', 20, 100, 0, 5, N'20 - 100 kg', N'0 - 5 km', 120000),
(N'Nhanh', 20, 100, 5, 10, N'20 - 100 kg', N'5 - 10 km', 140000),
(N'Nhanh', 20, 100, 10, 20, N'20 - 100 kg', N'10 - 20 km', 160000),
(N'Nhanh', 20, 100, 20, 50, N'20 - 100 kg', N'20 - 50 km', 190000);
SELECT *
FROM AspNetUsers;
Use DeliveryManagementDb
DELETE FROM AspNetUserRoles
WHERE UserId = (
    SELECT Id
    FROM AspNetUsers
    WHERE Email = N'admin@delivery.com'
);

DELETE FROM AspNetUsers
WHERE Email = N'admin@delivery.com';
SELECT Id, Email
FROM AspNetUsers
WHERE Email = N'admin@delivery.com';
SELECT Email, PasswordHash
FROM AspNetUsers
WHERE Email = N'admin@delivery.com';