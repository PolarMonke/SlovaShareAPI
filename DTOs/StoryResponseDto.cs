public class StoryResponseDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    
    // Owner information
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
