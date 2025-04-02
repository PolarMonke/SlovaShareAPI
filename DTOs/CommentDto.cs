public class CommentDto
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public AuthorDto? Author { get; set; }
    public DateTime CreatedAt { get; set; }
}