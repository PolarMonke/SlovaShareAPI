using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BCrypt.Net;

namespace Backend.Controllers
{
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
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: /Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }

        // рэгістрацыя
        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromBody] User authData)
        {
            if (await _context.Users.AnyAsync(u => u.Email == authData.Email))
            {
                return BadRequest(new { Message = "Карыстальнік з такім імэйлам ужо існуе" });
            }

            var newUser = new User
            {
                Email = authData.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(authData.PasswordHash)
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            var userProfile = new UserData
            {
                Id = newUser.Id,
                Description = null,
                ProfileImage = null
            };

            _context.UserData.Add(userProfile);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Карыстальнік зарэгістраваны", UserId = newUser.Id });
        }

        // абнаўленне профілю
        [HttpPut("profile/{userId}")]
        public async Task<IActionResult> UpdateProfile(int userId, [FromBody] UserData updatedProfile)
        {
            var userProfile = await _context.UserData.FindAsync(userId);

            if (userProfile == null)
            {
                return NotFound(new { Message = "Карыстальнік не знойдзены" });
            }

            userProfile.Description = updatedProfile.Description;
            userProfile.ProfileImage = updatedProfile.ProfileImage;

            _context.UserData.Update(userProfile);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Профіль абноўлены", UserProfile = userProfile });
        }
        
        // POST: /Users
        [HttpPost]
        public async Task<ActionResult<User>> CreateUser(User user)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        // PUT: /Users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(u => u.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: /Users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}