public class StorySearchResultDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IEnumerable<StoryResponseDto>? Results { get; set; }
}