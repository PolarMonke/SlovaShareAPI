using System.ComponentModel.DataAnnotations;

public class StoryPartCreateDto
{
    [Required, MinLength(10)]
    public string? Content { get; set; }
}