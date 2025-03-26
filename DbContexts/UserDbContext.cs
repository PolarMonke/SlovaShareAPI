using Microsoft.EntityFrameworkCore;

namespace Backend
{
    public class UserDbContext : DbContext
    {
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<UserData> UserData { get; set; }
        public virtual DbSet<UserStatistics> UserStatistics { get; set; }

        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>()
                .HasOne(u => u.UserData)
                .WithOne(ud => ud.User)
                .HasForeignKey<UserData>(ud => ud.UserId);

            modelBuilder.Entity<User>()
                .HasOne(u => u.UserStatistics)
                .WithOne(us => us.User)
                .HasForeignKey<UserStatistics>(us => us.UserId);
        }
    }
}