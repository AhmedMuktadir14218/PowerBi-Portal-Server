using CRUD_Api.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace CRUD_Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Fix: Change this line to use DbContextOptions<ApplicationDbContext>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Employee>()
                .Property(e => e.Salary)
                .HasColumnType("decimal(18,2)");

            // Configure UserCategoryPermission relationships
            modelBuilder.Entity<UserCategoryPermission>()
                .HasOne(ucp => ucp.User)
                .WithMany()
                .HasForeignKey(ucp => ucp.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserCategoryPermission>()
                .HasOne(ucp => ucp.Category)
                .WithMany(c => c.UserPermissions)
                .HasForeignKey(ucp => ucp.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserCategoryPermission>()
                .HasOne(ucp => ucp.GrantedBy)
                .WithMany()
                .HasForeignKey(ucp => ucp.GrantedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Category relationships
            modelBuilder.Entity<Category>()
                .HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Unique constraint to prevent duplicate permissions
            modelBuilder.Entity<UserCategoryPermission>()
                .HasIndex(ucp => new { ucp.UserId, ucp.CategoryId })
                .IsUnique();
        }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserLogin> UserLogins { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<UserCategoryPermission> UserCategoryPermissions { get; set; }
    }
}