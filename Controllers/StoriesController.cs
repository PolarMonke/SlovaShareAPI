using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;

[ApiController]
[Route("[controller]")]
[Authorize]
public class StoriesController : ControllerBase
{
    private readonly StoryDbContext _context;
    private readonly UserDbContext _userContext;

    public StoriesController(StoryDbContext context, UserDbContext userContext)
    {
        _context = context;
        _userContext = userContext;
    }

    //stories/
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StoryResponseDto>>> GetStories()
    {
        return await _context.Stories
        .Include(s => s.Owner)
        .Include(s => s.StoryTags)
        .ThenInclude(st => st.Tag)
        .Select(s => new StoryResponseDto
        {
            Id = s.Id,
            Title = s.Title ?? string.Empty,
            Description = s.Description ?? string.Empty,
            Owner = s.Owner != null ? new UserResponseDto 
            {
                Id = s.Owner.Id,
                Email = s.Owner.Email ?? string.Empty,
                Login = s.Owner.Login ?? string.Empty,
                CreatedAt = s.Owner.CreatedAt
            } : null,
            IsPublic = s.IsPublic,
            CoverImageUrl = s.CoverImageUrl ?? string.Empty,
            Tags = s.StoryTags.Select(st => st.Tag.Name ?? string.Empty).ToList(),
            PartsCount = s.Parts.Count,
            LikeCount = s.Likes.Count,
            CommentCount = s.Comments.Count,
            CreatedAt = s.CreatedAt
        })
        .ToListAsync();
    }

    // stories/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<StoryResponseDto>> GetStory(int id)
    {
        var story = await _context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Parts.OrderBy(p => p.Order))
                .ThenInclude(p => p.Author)
            .Include(s => s.StoryTags)
                .ThenInclude(st => st.Tag)
            .Include(s => s.Likes)
            .Include(s => s.Comments)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (story == null)
        {
            return NotFound();
        }

        var userId = GetUserId();
        if (!story.IsPublic && story.OwnerId != userId)
        {
            return Forbid();
        }

        return new StoryResponseDto
        {
            Id = story.Id,
            Title = story.Title ?? string.Empty,
            Description = story.Description ?? string.Empty,
            IsPublic = story.IsPublic,
            CoverImageUrl = story.CoverImageUrl ?? string.Empty,
            CreatedAt = story.CreatedAt,
            UpdatedAt = story.UpdatedAt,

            Owner = new UserResponseDto
            {
                Id = story.Owner.Id,
                Email = story.Owner.Email ?? string.Empty,
                Login = story.Owner.Login ?? string.Empty,
                CreatedAt = story.Owner.CreatedAt
            },

            Parts = story.Parts.Select(p => new StoryPartResponseDto
            {
                Id = p.Id,
                Content = p.Content ?? string.Empty,
                Order = p.Order,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                Author = new UserResponseDto
                {
                    Id = p.Author.Id,
                    Email = p.Author.Email ?? string.Empty,
                    Login = p.Author.Login ?? string.Empty,
                    CreatedAt = p.Author.CreatedAt
                }
            }).ToList(),

            Tags = story.StoryTags
                .Select(st => st.Tag?.Name ?? string.Empty)
                .ToList(),

            LikeCount = story.Likes.Count,
            CommentCount = story.Comments.Count,

            Contributors = story.Parts
                .GroupBy(p => p.AuthorId)
                .Select(g => new UserResponseDto
                {
                    Id = g.First().Author.Id,
                    Email = g.First().Author.Email ?? string.Empty,
                    Login = g.First().Author.Login ?? string.Empty,
                    CreatedAt = g.First().Author.CreatedAt
                })
                .ToList()
        };
    }


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

        return userId;
    }
}