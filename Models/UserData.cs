using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Backend;

public class UserData
{
    public int Id { get; set; }

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string ProfileImage { get; set; } = string.Empty;

    public int UserId { get; set; }
    
    [JsonIgnore]
    public User? User { get; set; }
}