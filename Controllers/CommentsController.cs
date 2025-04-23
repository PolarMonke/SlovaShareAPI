using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend;

[ApiController]
[Route("stories/{id}/[controller]")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CommentsController(AppDbContext context)
    {
        _context = context;
    }

    #region Comments

    [HttpPost]
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

        var author = await _context.Users.FindAsync(userId);

        if (story.OwnerId != userId)
        {
            var ownerStats = await _context.UserStatistics.FirstOrDefaultAsync(s => s.UserId == story.OwnerId);
            if (ownerStats != null) ownerStats.CommentsReceived++;
            await _context.SaveChangesAsync();
        }

        return CreatedAtAction(
            nameof(GetComment), 
            new { id, commentId = comment.Id }, 
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

    [HttpGet]
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
    [HttpGet("{commentId}")]
    public async Task<ActionResult<CommentResponseDto>> GetComment(int id, int commentId)
    {
        var comment = await _context.Comments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.StoryId == id);

        if (comment == null) return NotFound();

        return new CommentResponseDto
        {
            Id = comment.Id,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            Author = new UserResponseDto
            {
                Id = comment.User.Id,
                Login = comment.User.Login,
                Email = comment.User.Email,
                CreatedAt = comment.User.CreatedAt
            }
        };
    }

    [HttpDelete("{commentId}")]
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