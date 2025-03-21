using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class StoryPart
{
    public int Id { get; set; }

    public int StoryId { get; set; }

    public int AuthorId { get; set; }

    [StringLength(10000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Story? Story { get; set; }
    public User? Author { get; set; }
}