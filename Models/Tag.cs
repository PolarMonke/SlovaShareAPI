using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class Tag
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string? Name { get; set; }

    public ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();
}