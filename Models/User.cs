using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Backend;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    [Column("Login")]
    public string? Login { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Column("Email")]
    public string? Email { get; set; }

    [Required]
    [StringLength(100)]
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public UserData? UserData { get; set; }
    [JsonIgnore]
    public UserStatistics? UserStatistics { get; set; }
}