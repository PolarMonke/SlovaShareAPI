using System.ComponentModel.DataAnnotations;
public class UserCreateDto
{
    [Required, EmailAddress, StringLength(100)]
    public string? Email { get; set; }

    [Required, StringLength(50)]
    public string? Login { get; set; }

    [Required, StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }
}