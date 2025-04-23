using System.ComponentModel.DataAnnotations;
public class StoryUpdateDto
{
    [StringLength(100)]
    public string? Title { get; set; }
    
    [StringLength(5000)]
    public string? Description { get; set; }
    
    public bool? IsPublic { get; set; }
    public string? CoverImageUrl { get; set; }
    public string[]? Tags { get; set; }
}