using System.ComponentModel.DataAnnotations;

public class CommentResponseDto
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public UserResponseDto? Author { get; set; }
    public DateTime CreatedAt { get; set; }
}