using System.ComponentModel.DataAnnotations;

public class CommentCreateDto
{
    [Required, MinLength(1), MaxLength(1000)]
    public string? Content { get; set; }
}