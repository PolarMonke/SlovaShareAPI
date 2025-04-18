public class UserProfileDto
{
    public int Id { get; set; }
    public string Login { get; set; }
    public string Description { get; set; }
    public string ProfileImage { get; set; }
    public int StoriesStarted { get; set; }
    public int StoriesContributed { get; set; }
    public int LikesReceived { get; set; }
    public int CommentsReceived { get; set; }
    public bool IsCurrentUser { get; set; }
}