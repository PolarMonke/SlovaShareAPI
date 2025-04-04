using System.ComponentModel.DataAnnotations;

public class CommentCreateDto
{
    [Required, MinLength(1), MaxLength(1000)]
    public string? Content { get; set; }
}

public class CommentResponseDto
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public UserResponseDto? Author { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LikeResponseDto
{
    public int StoryId { get; set; }
    public UserResponseDto? User { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReportCreateDto
{
    [Required, MaxLength(100)]
    public string? Reason { get; set; }
    
    [MaxLength(1000)]
    public string? Details { get; set; }
}