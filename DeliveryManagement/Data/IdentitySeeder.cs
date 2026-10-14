using Microsoft.AspNetCore.Identity;

namespace DeliveryManagement.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
                "Customer",
                "ReceptionStaff",
                "WarehouseStaff",
                "DeliveryStaff",
                "Admin"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }
        }
    }
}