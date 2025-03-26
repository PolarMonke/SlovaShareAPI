using System.ComponentModel.DataAnnotations;

public class LoginDto
{
    [Required]
    public string? Login { get; set; }
    
    [Required]
    public string? Password { get; set; }
    
}