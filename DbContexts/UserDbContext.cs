using Microsoft.EntityFrameworkCore;

namespace Backend
{
    public class UserDbContext : DbContext
    {
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserData> UserData { get; set; }

        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        }
    }
}