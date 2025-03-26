using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public User? User { get; set; }
}