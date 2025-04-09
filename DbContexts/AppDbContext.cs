using Microsoft.EntityFrameworkCore;
namespace Backend;
public class AppDbContext : DbContext
{
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserData> UserData { get; set; }
    public virtual DbSet<UserStatistics> UserStatistics { get; set; }
    


    public virtual DbSet<Story> Stories { get; set; }
    public virtual DbSet<StoryPart> StoryParts { get; set; }
    public virtual DbSet<Tag> Tags { get; set; }
    public virtual DbSet<StoryTag> StoryTags { get; set; }
    public virtual DbSet<Like> Likes { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Report> Reports { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

        modelBuilder.Entity<StoryTag>()
            .HasKey(st => new { st.StoryId, st.TagId });
        modelBuilder.Entity<StoryTag>()
            .HasOne(st => st.Story)
            .WithMany(s => s.StoryTags)
            .HasForeignKey(st => st.StoryId);
        modelBuilder.Entity<StoryTag>()
            .HasOne(st => st.Tag)
            .WithMany(t => t.StoryTags)
            .HasForeignKey(st => st.TagId);
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.Property(t => t.Name)
                .HasMaxLength(255);
            entity.HasIndex(t => t.Name)
                .IsUnique();
        });
    }
}