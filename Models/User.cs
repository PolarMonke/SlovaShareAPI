using System.ComponentModel.DataAnnotations;

namespace Backend;

public class User
{
    public int Id { get; set; }
    [Required]
    [StringLength(50)]
    public string Login { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; }

    [Required]
    [StringLength(100)]
    public string PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
