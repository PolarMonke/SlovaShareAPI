using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend;

public class Story
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public int OwnerId { get; set; }

    [ForeignKey("AuthorId")]
    public User? Owner { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; } = true;

    public bool IsEditable { get; set; } = true;

    public bool IsCompleted { get; set; } = false;

    [StringLength(5000)]
    public string? Description { get; set; }

    [StringLength(255)]
    public string? CoverImageUrl { get; set; } 
    public ICollection<StoryPart> Parts { get; set; } = new List<StoryPart>();
    public ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();

    public void UpdateTimestamps()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}