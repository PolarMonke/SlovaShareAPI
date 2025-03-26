using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Backend;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Login { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public UserData? UserData { get; set; }
    [JsonIgnore]
    public UserStatistics? UserStatistics { get; set; }
}