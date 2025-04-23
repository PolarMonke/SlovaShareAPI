using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend;

[ApiController]
[Route("stories/{id}/[controller]")]
[Authorize]
public class LikesController : ControllerBase
{
    private readonly AppDbContext _context;

    public LikesController(AppDbContext context)
    {
        _context = context;
    }
    #region Social features

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ToggleLike(int id)
    {
        var userId = GetUserId();
        var story = await _context.Stories.FindAsync(id);
        if (story == null) return NotFound();

        var like = await _context.Likes.FirstOrDefaultAsync(l => l.StoryId == id && l.UserId == userId);
        
        if (like != null)
        {
            _context.Likes.Remove(like);
            var ownerStats = await _context.UserStatistics.FirstOrDefaultAsync(s => s.UserId == story.OwnerId);
            if (ownerStats != null) ownerStats.LikesReceived--;
        }
        else
        {
            _context.Likes.Add(new Like { StoryId = id, UserId = userId });
            var ownerStats = await _context.UserStatistics.FirstOrDefaultAsync(s => s.UserId == story.OwnerId);
            if (ownerStats != null) ownerStats.LikesReceived++;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<LikeStatusDto>> GetLikeStatus(int id)
    {
        var userId = GetUserId();
        
        var isLiked = await _context.Likes
            .AnyAsync(l => l.StoryId == id && l.UserId == userId);
            
        return Ok(new LikeStatusDto { IsLiked = isLiked });
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