using System.ComponentModel.DataAnnotations;

public class StoryPartUpdateDto
{
    [Required, MinLength(10)]
    public string? Content { get; set; }
}