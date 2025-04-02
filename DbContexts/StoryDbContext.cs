using Microsoft.EntityFrameworkCore;

namespace Backend
{
    public class StoryDbContext : DbContext
    {
        public virtual DbSet<Story> Stories { get; set; }
        public virtual DbSet<StoryPart> StoryParts { get; set; }
        public virtual DbSet<Tag> Tags { get; set; }
        public virtual DbSet<StoryTag> StoryTags { get; set; }
        public virtual DbSet<Like> Likes { get; set; }
        public virtual DbSet<Comment> Comments { get; set; }
        public virtual DbSet<Report> Reports { get; set; }

        public StoryDbContext(DbContextOptions<StoryDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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

            modelBuilder.Entity<Tag>()
                .HasIndex(t => t.Name)
                .IsUnique();
        }
    }
}