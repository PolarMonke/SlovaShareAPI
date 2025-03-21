using System.ComponentModel.DataAnnotations;

namespace Backend;

public class Story
{
    public int Id { get; set; }

    [Required]
    [StringLength(1000)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public int PublisherId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; }

    public bool IsEditable { get; set; }

    public bool IsClosed { get; set; }
    public User? Publisher { get; set; }
    public ICollection<StoryPart> StoryParts { get; set; } = new List<StoryPart>();

    public void UpdateTime()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}