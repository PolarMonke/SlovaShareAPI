using System.ComponentModel.DataAnnotations;

public class LikeResponseDto
{
    public int StoryId { get; set; }
    public UserResponseDto? User { get; set; }
    public DateTime CreatedAt { get; set; }
}