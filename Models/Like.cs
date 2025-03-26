using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class Like
{
    [Required]
    public int Id { get; set; }
    [Required]
    public int StoryId { get; set; }
    [Required]
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Story? Story { get; set; }
    public User? User { get; set; }
}