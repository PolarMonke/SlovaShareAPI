using System.ComponentModel.DataAnnotations;
public class UserCreateDto
{
    [Required, EmailAddress, StringLength(100)]
    public string? Email = string.Empty;

    [Required, StringLength(50)]
    public string? Login = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string? Password = string.Empty;
}