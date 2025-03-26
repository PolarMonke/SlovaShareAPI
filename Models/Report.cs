using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class Report
{
    public int Id { get; set; }
    [Required]
    public int StoryId { get; set; }
    [Required]
    public int UserId { get; set; }

    [StringLength(100)]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Story? Story { get; set; }
    public User? User { get; set; }
}