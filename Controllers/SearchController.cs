using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend;

[ApiController]
[Route("[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _context;

    public SearchController(AppDbContext context)
    {
        _context = context;
    }

    #region Search

    [HttpGet]
    [AllowAnonymous] 
    public async Task<ActionResult<StorySearchResultDto>> SearchStories(
        [FromQuery] string? query, 
        [FromQuery] string[]? tags,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var userId = User.Identity?.IsAuthenticated == true ? GetUserId() : -1;
        
        var storiesQuery = _context.Stories
            .Include(s => s.Owner)
            .Include(s => s.StoryTags)
                .ThenInclude(st => st.Tag)
            .Where(s => s.IsPublic || s.OwnerId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerm = query.Trim();
            storiesQuery = storiesQuery.Where(s =>
                s.Title.Contains(searchTerm) ||
                s.Description.Contains(searchTerm) ||
                s.Parts.Any(p => p.Content.Contains(searchTerm)));
        }

        if (tags != null && tags.Length > 0)
        {
            var normalizedTags = tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLower())
                .Distinct()
                .ToList();

            if (normalizedTags.Any())
            {
                storiesQuery = storiesQuery.Where(s =>
                    s.StoryTags.Any(st => 
                        normalizedTags.Contains(st.Tag.Name.ToLower())));
            }
        }

        var totalCount = await storiesQuery.CountAsync();

        var stories = await storiesQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StoryResponseDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                Owner = new UserResponseDto
                {
                    Id = s.Owner.Id,
                    Login = s.Owner.Login,
                    Email = s.Owner.Email,
                    CreatedAt = s.Owner.CreatedAt
                },
                IsPublic = s.IsPublic,
                CoverImageUrl = s.CoverImageUrl,
                Tags = s.StoryTags.Select(st => st.Tag.Name).ToList(),
                PartsCount = s.Parts.Count,
                LikeCount = s.Likes.Count,
                CommentCount = s.Comments.Count,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();

        return Ok(new StorySearchResultDto
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Results = stories
        });
    }

    #endregion

    #region Helping methods
    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedAccessException("User ID not found in claims");
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID format");
        }
        System.Console.WriteLine(userId);
        return userId;
    }
    #endregion
}

