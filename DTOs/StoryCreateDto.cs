using System.ComponentModel.DataAnnotations;

public class StoryCreateDto
{
    [Required, StringLength(100)]
    public string? Title { get; set; }
    
    [StringLength(5000)]
    public string? Description { get; set; }
    
    public bool IsPublic { get; set; } = true;
    public string? CoverImageUrl { get; set; }
    public string[] StoryTags { get; set; } = Array.Empty<string>();
    public string? InitialContent { get; set; }
}
