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
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [Authorize]
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
    [Authorize]
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
    [Authorize]
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

    #region Story parts

    [HttpPost("{storyId}/parts")]
    public async Task<ActionResult<StoryPartResponseDto>> AddPart(int storyId, StoryPartCreateDto partDto)
    {
        var userId = GetUserId();

        var story = await _context.Stories
        .Include(s => s.Parts)
        .FirstOrDefaultAsync(s => s.Id == storyId);
        if (story == null)
        {
            return NotFound(new { Message = "Story not found" });
        }
        if (!story.IsEditable)
        {
            return BadRequest(new { Message = "This story is not currently editable" });
        }

        var nextOrder = story.Parts.Any() ? story.Parts.Max(p => p.Order) + 1 : 1;

        var newPart = new StoryPart
        {
            Content = partDto.Content?.Trim() ?? throw new ArgumentNullException(nameof(partDto.Content)),
            Order = nextOrder,
            AuthorId = userId,
            StoryId = storyId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

         _context.StoryParts.Add(newPart);
        story.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return CreatedAtAction(
        nameof(GetStory), 
        new { id = storyId }, 
        new StoryPartResponseDto
        {
            Id = newPart.Id,
            Content = newPart.Content,
            Order = newPart.Order,
            CreatedAt = newPart.CreatedAt,
            UpdatedAt = newPart.UpdatedAt,
            Author = new UserResponseDto
            {
                Id = userId,
                Login = User.Identity?.Name ?? string.Empty
            }
        });

    }
    [HttpPut("{storyId}/parts/{partId}")]
    [Authorize]
    public async Task<IActionResult> UpdatePart(int storyId, int partId, StoryPartUpdateDto partDto)
    {
        var userId = GetUserId();
        
        var part = await _context.StoryParts
            .Include(p => p.Story)
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == partId && p.StoryId == storyId);

        if (part == null)
        {
            return NotFound(new { Message = "Part not found" });
        }

        if (part.AuthorId != userId && part.Story.OwnerId != userId)
        {
            return Forbid();
        }

        part.Content = partDto.Content?.Trim() ?? throw new ArgumentNullException(nameof(partDto.Content));
        part.UpdatedAt = DateTime.UtcNow;
        part.Story.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }
    [HttpDelete("{storyId}/parts/{partId}")]
    public async Task<IActionResult> DeletePart(int storyId, int partId)
    {
        var userId = GetUserId();

        var part = await _context.StoryParts
            .Include(p => p.Story)
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == partId && p.StoryId == storyId);

        if (part == null)
        {
            return NotFound(new { Message = "Part not found" });
        }

        if (part.AuthorId != userId && part.Story.OwnerId != userId)
        {
            return Forbid();
        }

        var subsequentParts = await _context.StoryParts
            .Where(p => p.StoryId == storyId && p.Order > part.Order)
            .ToListAsync();

        foreach (var subsequentPart in subsequentParts)
        {
            subsequentPart.Order--;
        }

        _context.StoryParts.Remove(part);
        part.Story.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }
    [HttpPut("{storyId}/parts/order")]
    public async Task<IActionResult> ReorderParts(int storyId, int[] partIdsInOrder)
    {
        var userId = GetUserId();

        var story = await _context.Stories.FindAsync(storyId);
        if (story == null)
        {
            return NotFound(new { Message = "Story not found" });
        }

        if (story.OwnerId != userId)
        {
            return Forbid();
        }

        var parts = await _context.StoryParts
            .Where(p => p.StoryId == storyId)
            .ToListAsync();

        var invalidParts = partIdsInOrder.Except(parts.Select(p => p.Id)).ToList();
        if (invalidParts.Any())
        {
            return BadRequest(new { Message = $"Invalid part IDs: {string.Join(",", invalidParts)}" });
        }

        if (parts.Count != partIdsInOrder.Length)
        {
            return BadRequest(new { Message = "Part count mismatch" });
        }

        for (int i = 0; i < partIdsInOrder.Length; i++)
        {
            var part = parts.First(p => p.Id == partIdsInOrder[i]);
            part.Order = i + 1;
        }

        story.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    #endregion

    #region Social features

    [HttpPost("{id}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleLike(int id)
    {
        var userId = GetUserId();

        var story = await _context.Stories.FindAsync(id);
        if (story == null)
        {
            return NotFound(new { Message = "Story not found" });
        }

        var like = await _context.Likes.FirstOrDefaultAsync(l => l.StoryId == id && l.UserId == userId);
        
        if (like != null)
        {
            _context.Likes.Remove(like);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Like removed", Liked = false });
        }
        else
        {
            var newLike = new Like
            {
                StoryId = id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Likes.Add(newLike);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Story liked", Liked = true });
        }
    }

    [HttpPost("{id}/comments")]
    [Authorize]
    public async Task<ActionResult<CommentResponseDto>> AddComment(int id, CommentCreateDto commentDto)
    {
        var userId = GetUserId();

        var story = await _context.Stories.FindAsync(id);
        if (story == null)
        {
            return NotFound(new { Message = "Story not found" });
        }
        var comment = new Comment
        {
            Content = commentDto.Content?.Trim() ?? throw new ArgumentNullException(nameof(commentDto.Content)),
            StoryId = id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Comments.Add(comment);
        story.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var author = await _userContext.Users.FindAsync(userId);

        return CreatedAtAction(
            nameof(GetStory), 
            new { id }, 
            new CommentResponseDto
            {
                Id = comment.Id,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                Author = new UserResponseDto
                {
                    Id = userId,
                    Login = author?.Login ?? string.Empty,
                    Email = author?.Email ?? string.Empty,
                    CreatedAt = author?.CreatedAt ?? DateTime.MinValue
                }
            });

    }

    [HttpGet("{id}/comments")]
    public async Task<ActionResult<IEnumerable<CommentResponseDto>>> GetComments(int id)
    {
        var storyExists = await _context.Stories.AnyAsync(s => s.Id == id);
        if (!storyExists)
        {
            return NotFound(new { Message = "Story not found" });
        }

        var comments = await _context.Comments
            .Where(c => c.StoryId == id)
            .OrderByDescending(c => c.CreatedAt)
            .Include(c => c.User)
            .Select(c => new CommentResponseDto
            {
                Id = c.Id,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                Author = new UserResponseDto
                {
                    Id = c.User.Id,
                    Login = c.User.Login,
                    Email = c.User.Email,
                    CreatedAt = c.User.CreatedAt
                }
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpDelete("{id}/comments/{commentId}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int id, int commentId)
    {
        var userId = GetUserId();

        var comment = await _context.Comments
            .Include(c => c.User)
            .Include(c => c.Story)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.StoryId == id);

        if (comment == null)
        {
            return NotFound(new { Message = "Comment not found" });
        }

        if (comment.UserId != userId && comment.Story.OwnerId != userId)
        {
            return Forbid();
        }

        _context.Comments.Remove(comment);
        comment.Story.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    #endregion
    
    #region Search

    [HttpGet("search")]
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

    #region User specific

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<StoryResponseDto>>> GetUserStories(int userId)
    {
        var userExists = await _userContext.Users.AnyAsync(u => u.Id == userId);
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
    [HttpGet("user/{userId}/contributions")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<StoryResponseDto>>> GetUserContributions(int userId)
    {
        var userExists = await _userContext.Users.AnyAsync(u => u.Id == userId);
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

    #region Reports

    [HttpPost("{id}/report")]
    public async Task<IActionResult> ReportStory(int id, ReportCreateDto reportDto)
    {
        var userId = GetUserId();

        var story = await _context.Stories.FindAsync(id);
        if (story == null)
        {
            return NotFound(new { Message = "Story not found" });
        }

        var existingReport = await _context.Reports
            .FirstOrDefaultAsync(r => r.StoryId == id && r.UserId == userId);

        if (existingReport != null)
        {
            return BadRequest(new { Message = "You have already reported this story" });
        }

        var report = new Report
        {
            StoryId = id,
            UserId = userId,
            Reason = reportDto.Reason?.Trim(),
            Content = reportDto.Details?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        // Notify moderator
        // Use some tg bot
        // Block or ignore through this bot

        return Ok(new { Message = "Story reported successfully" });
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