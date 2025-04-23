using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserStoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserStoriesController(AppDbContext context)
    {
        _context = context;
    }

    #region User Stories

    [HttpGet("{userId}")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<StoryResponseDto>>> GetUserStories(int userId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return NotFound(new { Message = "User not found" });
        }

        var currentUserId = User.Identity?.IsAuthenticated == true ? GetUserId() : -1;
        var isOwner = currentUserId == userId;

        var stories = await _context.Stories
            .Where(s => s.OwnerId == userId && (s.IsPublic || isOwner))
            .Include(s => s.StoryTags)
                .ThenInclude(st => st.Tag)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StoryResponseDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                IsPublic = s.IsPublic,
                CoverImageUrl = s.CoverImageUrl,
                Tags = s.StoryTags.Select(st => st.Tag.Name).ToList(),
                PartsCount = s.Parts.Count,
                LikeCount = s.Likes.Count,
                CommentCount = s.Comments.Count,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Owner = new UserResponseDto
                {
                    Id = userId,
                    Login = s.Owner.Login
                }
            })
            .ToListAsync();

        return Ok(stories);
    }
    [HttpGet("{userId}/contributions")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<StoryResponseDto>>> GetUserContributions(int userId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return NotFound(new { Message = "User not found" });
        }

        var currentUserId = User.Identity?.IsAuthenticated == true ? GetUserId() : -1;

        var contributedStories = await _context.StoryParts
            .Where(p => p.AuthorId == userId)
            .Select(p => p.Story)
            .Where(s => s.IsPublic || s.OwnerId == currentUserId)
            .Distinct()
            .Include(s => s.StoryTags)
                .ThenInclude(st => st.Tag)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StoryResponseDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                IsPublic = s.IsPublic,
                CoverImageUrl = s.CoverImageUrl,
                Tags = s.StoryTags.Select(st => st.Tag.Name).ToList(),
                PartsCount = s.Parts.Count,
                LikeCount = s.Likes.Count,
                CommentCount = s.Comments.Count,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                Owner = new UserResponseDto
                {
                    Id = s.OwnerId,
                    Login = s.Owner.Login
                }
            })
            .ToListAsync();

        return Ok(contributedStories);
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