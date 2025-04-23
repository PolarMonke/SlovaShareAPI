using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Backend;

[ApiController]
[Route("stories/{id}/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ReportsController(AppDbContext context)
    {
        _context = context;
    }
    #region Reports

    [HttpPost]
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
        System.Console.WriteLine(userId);
        return userId;
    }
    #endregion
}