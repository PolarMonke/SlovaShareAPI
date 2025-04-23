public class StoryPartResponseDto
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public int Order { get; set; }
    public UserResponseDto? Author { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}