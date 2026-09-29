using DeliveryManagement.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DeliveryManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Business tables
        public DbSet<DeliveryOrder> DeliveryOrders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<PackageType> PackageTypes { get; set; }

        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

        public DbSet<MapStation> MapStations { get; set; }

        public DbSet<CheckIn> CheckIns { get; set; }

        public DbSet<Review> Reviews { get; set; }

        public DbSet<Conversation> Conversations { get; set; }

        public DbSet<Message> Messages { get; set; }

        public DbSet<PriceTable> PriceTables { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // DeliveryOrder → Customer
            builder.Entity<DeliveryOrder>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // DeliveryOrder → DeliveryStaff
            builder.Entity<DeliveryOrder>()
                .HasOne(o => o.DeliveryStaff)
                .WithMany()
                .HasForeignKey(o => o.DeliveryStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrderItem → DeliveryOrder
            builder.Entity<OrderItem>()
                .HasOne(i => i.DeliveryOrder)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(i => i.OrderCode)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderItem → PackageType
            builder.Entity<OrderItem>()
                .HasOne(i => i.PackageType)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.PackageTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrderStatusHistory → DeliveryOrder
            builder.Entity<OrderStatusHistory>()
                .HasOne(h => h.DeliveryOrder)
                .WithMany(o => o.OrderStatusHistories)
                .HasForeignKey(h => h.OrderCode)
                .OnDelete(DeleteBehavior.Cascade);

            // CheckIn → DeliveryOrder
            builder.Entity<CheckIn>()
                .HasOne(c => c.DeliveryOrder)
                .WithMany()
                .HasForeignKey(c => c.OrderCode)
                .OnDelete(DeleteBehavior.Cascade);

            // CheckIn → MapStation
            builder.Entity<CheckIn>()
                .HasOne(c => c.MapStation)
                .WithMany(s => s.CheckIns)
                .HasForeignKey(c => c.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            // CheckIn → DeliveryStaff
            builder.Entity<CheckIn>()
                .HasOne(c => c.DeliveryStaff)
                .WithMany()
                .HasForeignKey(c => c.DeliveryStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // Review → DeliveryOrder
            builder.Entity<Review>()
                .HasOne(r => r.DeliveryOrder)
                .WithMany()
                .HasForeignKey(r => r.OrderCode)
                .OnDelete(DeleteBehavior.Cascade);

            // Review → Customer
            builder.Entity<Review>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Review → Staff
            builder.Entity<Review>()
                .HasOne(r => r.Staff)
                .WithMany()
                .HasForeignKey(r => r.RespondedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Conversation → Customer
            builder.Entity<Conversation>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Conversation → Staff
            builder.Entity<Conversation>()
                .HasOne(c => c.Staff)
                .WithMany()
                .HasForeignKey(c => c.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            // Message → Conversation
            builder.Entity<Message>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Message → Sender
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Decimal precision
            builder.Entity<DeliveryOrder>()
                .Property(o => o.TotalAmount)
                .HasPrecision(18, 2);

            builder.Entity<DeliveryOrder>()
                .Property(o => o.PaymentAmount)
                .HasPrecision(18, 2);

            builder.Entity<OrderItem>()
                .Property(i => i.Weight)
                .HasPrecision(10, 2);

            builder.Entity<CheckIn>()
                .Property(c => c.Latitude)
                .HasPrecision(10, 7);

            builder.Entity<CheckIn>()
                .Property(c => c.Longitude)
                .HasPrecision(10, 7);

            builder.Entity<MapStation>()
                .Property(s => s.Latitude)
                .HasPrecision(10, 7);

            builder.Entity<MapStation>()
                .Property(s => s.Longitude)
                .HasPrecision(10, 7);

            builder.Entity<PriceTable>()
                .Property(p => p.Price)
                .HasPrecision(18, 2);
        }
    }
}