using System.ComponentModel.DataAnnotations;

public class StoryPartCreateDto
{
    [Required, MinLength(10)]
    public string? Content { get; set; }
}
public class StoryPartUpdateDto
{
    [Required, MinLength(10)]
    public string? Content { get; set; }
}
public class StoryPartResponseDto
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public int Order { get; set; }
    public UserResponseDto? Author { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}