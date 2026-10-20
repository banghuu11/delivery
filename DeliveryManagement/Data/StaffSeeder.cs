
using DeliveryManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace DeliveryManagement.Data
{
    public static class StaffSeeder
    {
        public static async Task SeedStaffAsync(
            UserManager<ApplicationUser> userManager)
        {
            await CreateStaffAsync(
    userManager,
    "reception@delivery.com",
    "Staff@123",
    "Nhân viên tiếp nhận",
    "ReceptionStaff");

            await CreateStaffAsync(
                userManager,
                "warehouse@delivery.com",
                "Staff@123",
                "Nhân viên kho",
                "WarehouseStaff");

            await CreateStaffAsync(
                userManager,
                "shipper@delivery.com",
                "Staff@123",
                "Nhân viên giao hàng",
                "DeliveryStaff");

            await CreateStaffAsync(
                userManager,
                "customer@delivery.com",
                "Customer@123",
                "Khách hàng mẫu",
                "Customer");
        }

        private static async Task CreateStaffAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string fullName,
            string role)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName
                };

                var result = await userManager.CreateAsync(
                    user,
                    password);

                if (!result.Succeeded)
                {
                    throw new Exception(
                        string.Join(
                            "; ",
                            result.Errors.Select(e => e.Description)
                        )
                    );
                }
            }
            else
            {
                // Ensure password matches expected demo password
                if (!await userManager.CheckPasswordAsync(user, password))
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    await userManager.ResetPasswordAsync(user, token, password);
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                var result = await userManager.AddToRoleAsync(user, role);

                if (!result.Succeeded)
                {
                    throw new Exception(
                        string.Join(
                            "; ",
                            result.Errors.Select(e => e.Description)
                        )
                    );
                }
            }
        }
    }
}
