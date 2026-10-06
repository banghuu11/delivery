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
                "Reception@123",
                "Nhân viên tiếp nhận",
                "ReceptionStaff");

            await CreateStaffAsync(
                userManager,
                "warehouse@delivery.com",
                "Warehouse@123",
                "Nhân viên kho",
                "WarehouseStaff");
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

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}