using System.ComponentModel.DataAnnotations;

namespace Backend;

public class UserStatistics
{
    public int Id { get; set; }

    [Required]
    public int StoriesStarted { get; set; } = 0;

    [Required]
    public int StoriesContributed { get; set; } = 0;

    [Required]
    public int LikesReceived { get; set; } = 0;

    [Required]
    public int CommentsReceived { get; set; } = 0;

    public int UserId { get; set; }

    public User? User { get; set; }
}