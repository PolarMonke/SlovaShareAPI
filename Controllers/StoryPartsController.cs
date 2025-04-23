    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.AspNetCore.Authorization;
    using System.Security.Claims;
    using Backend;

    [ApiController]
    [Route("stories/{storyId}/[controller]")]
    [Authorize]
    public class StoryPartsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StoryPartsController(AppDbContext context)
        {
            _context = context;
        }

        #region Story parts

        [HttpPost]
        public async Task<ActionResult<StoryPartResponseDto>> AddPart(int storyId, StoryPartCreateDto partDto)
        {
            try
            {
                var userId = GetUserId();
                Console.WriteLine($"User {userId} attempting to add part to story {storyId}"); // Debug log

                var story = await _context.Stories
                    .Include(s => s.Parts)
                    .FirstOrDefaultAsync(s => s.Id == storyId);
                
                if (story == null)
                {
                    Console.WriteLine("Story not found"); // Debug log
                    return NotFound(new { Message = "Story not found" });
                }

                if (!story.IsEditable)
                {
                    Console.WriteLine("Story not editable"); // Debug log
                    return BadRequest(new { Message = "This story is not currently editable" });
                }

                // Validate content
                if (string.IsNullOrWhiteSpace(partDto.Content))
                {
                    Console.WriteLine("Empty content provided"); // Debug log
                    return BadRequest(new { Message = "Part content cannot be empty" });
                }

                // Update contributor stats if not the owner
                if (story.OwnerId != userId)
                {
                    var stats = await _context.UserStatistics.FirstOrDefaultAsync(s => s.UserId == userId);
                    if (stats != null)
                    {
                        stats.StoriesContributed++;
                        await _context.SaveChangesAsync();
                    }
                }

                var nextOrder = story.Parts.Any() ? story.Parts.Max(p => p.Order) + 1 : 1;

                var newPart = new StoryPart
                {
                    Content = partDto.Content.Trim(),
                    Order = nextOrder,
                    AuthorId = userId,
                    StoryId = storyId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.StoryParts.Add(newPart);
                story.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var author = await _context.Users.FindAsync(userId);

                return CreatedAtAction(
                    nameof(GetPart), 
                    new { storyId = storyId, partId = newPart.Id}, 
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
                            Login = author?.Login ?? string.Empty
                        }
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding part: {ex}");
                return StatusCode(500, new { Message = "Internal server error", Details = ex.Message });
            }
        }

        [HttpGet("{partId}")]
        [AllowAnonymous]
        public async Task<ActionResult<StoryPartResponseDto>> GetPart(int storyId, int partId)
        {
            try
            {
                var part = await _context.StoryParts
                    .Include(p => p.Author)
                    .Include(p => p.Story)
                    .FirstOrDefaultAsync(p => p.Id == partId && p.StoryId == storyId);

                if (part == null)
                {
                    return NotFound(new { Message = "Part not found" });
                }

                if (!part.Story.IsPublic)
                {
                    var userId = User.Identity?.IsAuthenticated == true ? GetUserId() : -1;
                    if (part.Story.OwnerId != userId)
                    {
                        return Unauthorized(new { Message = "You don't have access to this story part" });
                    }
                }

                return Ok(new StoryPartResponseDto
                {
                    Id = part.Id,
                    Content = part.Content,
                    Order = part.Order,
                    CreatedAt = part.CreatedAt,
                    UpdatedAt = part.UpdatedAt,
                    Author = new UserResponseDto
                    {
                        Id = part.Author.Id,
                        Login = part.Author.Login,
                        Email = part.Author.Email,
                        CreatedAt = part.Author.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "An error occurred while fetching the story part" });
            }
        }

        [HttpPut("{partId}")]
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
        [HttpDelete("{partId}")]
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
        [HttpPut("order")]
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