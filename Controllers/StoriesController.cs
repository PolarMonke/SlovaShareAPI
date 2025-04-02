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
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetStories()
    {
        return await _context.Stories
            .Select(s => new StoryResponseDto
            {
                Id = 
            })
            .ToListAsync();
    }
}