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
    #region Stories
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

    [HttpPost]
    public async Task<ActionResult<StoryResponseDto>> CreateStory(StoryCreateDto storyDto)
    {
        var userId = GetUserId();
        var story = new Story
        {
            Title = storyDto.Title?.Trim() ?? throw new ArgumentNullException(nameof(storyDto.Title)),
            Description = storyDto.Description?.Trim(),
            IsPublic = storyDto.IsPublic,
            CoverImageUrl = storyDto.CoverImageUrl?.Trim(),
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        if (storyDto.StoryTags != null && storyDto.StoryTags.Length > 0)
        {
            story.StoryTags = new List<StoryTag>();
            
            foreach (var tagName in storyDto.StoryTags)
            {
                if (string.IsNullOrWhiteSpace(tagName)) continue;
                
                var normalizedTagName = tagName.Trim().ToLower();
                
                var tag = await _context.Tags
                    .FirstOrDefaultAsync(t => t.Name == normalizedTagName) 
                    ?? new Tag { Name = normalizedTagName };
                
                story.StoryTags.Add(new StoryTag { Tag = tag });
            }
        }
        if (!string.IsNullOrWhiteSpace(storyDto.InitialContent))
        {
            story.Parts = new List<StoryPart>
            {
                new StoryPart
                {
                    Content = storyDto.InitialContent.Trim(),
                    Order = 1,
                    AuthorId = userId,
                    CreatedAt = DateTime.UtcNow
                }
            };
        }
        _context.Stories.Add(story);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetStory), new { id = story.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStory(int id, StoryUpdateDto storyDto)
    {
        var story = await _context.Stories
        .Include(s => s.StoryTags)
            .ThenInclude(st => st.Tag)
        .FirstOrDefaultAsync(s => s.Id == id);
        if (story == null)
        {
            return NotFound(new { Message = "Profile not found" });
        }
        var userId = GetUserId();
        if (story.OwnerId != userId)
        {
            return Forbid();
        }


        story.Title = storyDto.Title ?? story.Title;
        story.Description = storyDto.Description ?? story.Description;
        story.IsPublic = storyDto.IsPublic ?? story.IsPublic;
        story.CoverImageUrl = storyDto.CoverImageUrl ?? story.CoverImageUrl;
        story.UpdatedAt = DateTime.UtcNow;

        if (storyDto.Tags != null)
        {
            var tagsToRemove = story.StoryTags
                .Where(st => !storyDto.Tags.Contains(st.Tag.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var tagToRemove in tagsToRemove)
            {
                story.StoryTags.Remove(tagToRemove);
            }

            var normalizedNewTags = storyDto.Tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLower())
                .Distinct()
                .ToList();

            var currentTagNames = story.StoryTags
                .Select(st => st.Tag.Name.ToLower())
                .ToList();

            var tagsToAdd = normalizedNewTags
                .Where(tagName => !currentTagNames.Contains(tagName))
                .ToList();

            if (tagsToAdd.Any())
            {
                var existingTags = await _context.Tags
                    .Where(t => tagsToAdd.Contains(t.Name))
                    .ToListAsync();

                foreach (var tagName in tagsToAdd)
                {
                    var tag = existingTags.FirstOrDefault(t => t.Name == tagName) 
                        ?? new Tag { Name = tagName };
                    
                    story.StoryTags.Add(new StoryTag { Tag = tag });
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Profile updated" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStory(int id)
    {
        var userId = GetUserId();
    
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var story = await _context.Stories
                .Include(s => s.Parts)
                .Include(s => s.StoryTags)
                .Include(s => s.Likes)
                .Include(s => s.Comments)
                .Include(s => s.Reports)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null)
            {
                return NotFound();
            }
            if (story.OwnerId != userId)
            {
                return Forbid();
            }

            _context.StoryParts.RemoveRange(story.Parts);
            _context.StoryTags.RemoveRange(story.StoryTags);
            _context.Likes.RemoveRange(story.Likes);
            _context.Comments.RemoveRange(story.Comments);
            _context.Reports.RemoveRange(story.Reports);

            _context.Stories.Remove(story);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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

        return userId;
    }
    #endregion
}