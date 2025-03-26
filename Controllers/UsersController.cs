using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;

[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _context;

    public UsersController(UserDbContext context)
    {
        _context = context;
    }

    // GET: /Users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
    {
        return await _context.Users
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Email = u.Email,
                Login = u.Login,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();
    }

    // GET: /Users/5
    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponseDto>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound();
        }

        return new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            Login = user.Login,
            CreatedAt = user.CreatedAt
        };
    }

    [HttpPost("register")]
    public async Task<IActionResult> CreateUser([FromBody] UserCreateDto userDto)
    {
        Console.WriteLine($"Received: {userDto?.Login}, {userDto?.Email}");

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        if (string.IsNullOrEmpty(userDto.Login)) 
        {
            return BadRequest("Login is empty in DTO");
        }

        if (string.IsNullOrEmpty(userDto.Email))
        {
            return BadRequest("Email is empty in DTO");
        }

        if (await _context.Users.AnyAsync(u => u.Email == userDto.Email))
        {
            return BadRequest(new { Message = "User with this email already exists" });
        }

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newUser = new User
                {
                    Email = userDto.Email?.Trim() ?? throw new ArgumentNullException(nameof(userDto.Email)),
                    Login = userDto.Login?.Trim() ?? throw new ArgumentNullException(nameof(userDto.Login)),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(userDto.Password),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                var savedUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == newUser.Id);
                Console.WriteLine($"Saved user: {savedUser?.Login}, {savedUser?.Email}");

                _context.UserData.Add(new UserData
                {
                    UserId = newUser.Id,
                    Description = string.Empty,
                    ProfileImage = string.Empty
                });

                _context.UserStatistics.Add(new UserStatistics
                {
                    UserId = newUser.Id,
                    StoriesStarted = 0,
                    StoriesContributed = 0,
                    LikesReceived = 0,
                    CommentsReceived = 0
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetUser), 
                    new { id = newUser.Id }, 
                    new UserResponseDto
                    {
                        Id = newUser.Id,
                        Email = newUser.Email,
                        Login = newUser.Login,
                        CreatedAt = newUser.CreatedAt
                    });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new {
                    Message = "Invalid request",
                    Errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == loginDto.Login);

            if (user == null)
            {
                return Unauthorized(new { 
                    Message = "Invalid credentials",
                    Code = "INVALID_CREDENTIALS"
                });
            }

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                return Unauthorized(new { 
                    Message = "Invalid credentials",
                    Code = "INVALID_CREDENTIALS"
                });
            }

            return Ok(new {
                Id = user.Id,
                Login = user.Login,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                Message = "An error occurred during login",
                Error = ex.Message
            });
        }
    }

    [HttpPut("profile/{userId}")]
    public async Task<IActionResult> UpdateProfile(int userId, [FromBody] ProfileUpdateDto profileDto)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return NotFound(new { Message = "User not found" });
        }

        var userProfile = await _context.UserData.FirstOrDefaultAsync(ud => ud.UserId == userId);
        if (userProfile == null)
        {
            return NotFound(new { Message = "Profile not found" });
        }

        userProfile.Description = profileDto.Description ?? userProfile.Description;
        userProfile.ProfileImage = profileDto.ProfileImage ?? userProfile.ProfileImage;

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Profile updated" });
    }
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetProfile(int id)
    {
        var user = await _context.Users
            .Include(u => u.UserData)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        return Ok(new {
            login = user.Login,
            email = user.Email,
            description = user.UserData?.Description,
            profileImage = user.UserData?.ProfileImage
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto userDto)
    {
        var user = await _context.Users
            .Include(u => u.UserData)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        user.Login = userDto.Login ?? user.Login;
        user.Email = userDto.Email ?? user.Email;
        
        if (user.UserData != null)
        {
            user.UserData.Description = userDto.Description ?? user.UserData.Description;
            user.UserData.ProfileImage = userDto.ProfileImage ?? user.UserData.ProfileImage;
        }

        await _context.SaveChangesAsync();

        return Ok(new {
            login = user.Login,
            email = user.Email,
            description = user.UserData?.Description,
            profileImage = user.UserData?.ProfileImage
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var profile = await _context.UserData.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile != null)
            {
                _context.UserData.Remove(profile);
            }

            var stats = await _context.UserStatistics.FirstOrDefaultAsync(s => s.UserId == id);
            if (stats != null)
            {
                _context.UserStatistics.Remove(stats);
            }

            _context.Users.Remove(user);
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
}