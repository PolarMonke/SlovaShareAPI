using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class StoryPart
{
    public int Id { get; set; }
    [Required]
    public int StoryId { get; set; }
    [Required]
    public int AuthorId { get; set; }

    [Required]
    public int Order { get; set; }

    [StringLength(10000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Story? Story { get; set; }
    public User? Author { get; set; }
}