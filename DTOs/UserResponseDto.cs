using System.ComponentModel.DataAnnotations;

public class UserResponseDto
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? Login { get; set; }
    public DateTime CreatedAt { get; set; }
}