using Microsoft.AspNetCore.Identity;

namespace DeliveryManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        public string? DefaultSenderAddress { get; set; }

        public ICollection<SenderAddress> SenderAddresses { get; set; } = new List<SenderAddress>();
    }
}