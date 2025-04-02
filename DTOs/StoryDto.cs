using System.ComponentModel.DataAnnotations;

public class StoryCreateDto
{
    [Required, StringLength(100)]
    public string? Title { get; set; }
    
    [StringLength(5000)]
    public string? Description { get; set; }
    
    public bool IsPublic { get; set; } = true;
    public string? CoverImageUrl { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string? InitialContent { get; set; }
}
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

public class StoryResponseDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    
    // Owner information (using your existing UserResponseDto)
    public UserResponseDto? Owner { get; set; }
    
    // Collaboration metrics
    public int PartsCount { get; set; }
    public IEnumerable<UserResponseDto>? Contributors { get; set; }
    
    // Social metrics
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    
    // Story metadata
    public bool IsPublic { get; set; }
    public string? CoverImageUrl { get; set; }
    public IEnumerable<string>? Tags { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Optional: Full parts if needed (otherwise use PartsCount)
    public IEnumerable<StoryPartResponseDto>? Parts { get; set; }
}

