public class StorySearchResultDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IEnumerable<StoryResponseDto>? Results { get; set; }
}

public class TagSuggestionDto
{
    public string? Name { get; set; }
    public int UsageCount { get; set; }
}