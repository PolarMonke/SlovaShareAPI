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
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
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
        if (string.IsNullOrEmpty(userDto?.Login)) 
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

        var allUsers = await _context.Users.ToListAsync();

        var existingUserWithSamePassword = allUsers.FirstOrDefault(u => 
            BCrypt.Net.BCrypt.Verify(userDto.Password, u.PasswordHash));

        if (existingUserWithSamePassword != null)
        {
            return BadRequest(new { 
                Message = "Password already in use", 
                Login = existingUserWithSamePassword.Login,
                Field = "password"
            });
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
        var user = await _context.Users
            .Include(u => u.UserData)
            .FirstOrDefaultAsync(u => u.Login == loginDto.Login);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("your-32-character-secret-key-here");
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new {
            token = tokenString,
            user = new {
                id = user.Id,
                login = user.Login,
                email = user.Email,
                description = user.UserData?.Description,
                profileImage = user.UserData?.ProfileImage
            }
        });
    }
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users
            .Include(u => u.UserData)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound();

        return Ok(new {
            id = user.Id,
            login = user.Login,
            email = user.Email,
            description = user.UserData?.Description,
            profileImage = user.UserData?.ProfileImage
        });
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
    [Authorize]
    public async Task<ActionResult<UserProfileDto>> GetProfile(int id)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        
        var user = await _context.Users
            .Include(u => u.UserData)
            .Include(u => u.UserStatistics)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        return new UserProfileDto
        {
            Id = user.Id,
            Login = user.Login,
            Description = user.UserData?.Description ?? string.Empty,
            ProfileImage = user.UserData?.ProfileImage ?? string.Empty,
            StoriesStarted = user.UserStatistics?.StoriesStarted ?? 0,
            StoriesContributed = user.UserStatistics?.StoriesContributed ?? 0,
            LikesReceived = user.UserStatistics?.LikesReceived ?? 0,
            CommentsReceived = user.UserStatistics?.CommentsReceived ?? 0,
            IsCurrentUser = currentUserId == id
        };
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