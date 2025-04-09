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


namespace Backend;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;

    public UploadsController(
        IWebHostEnvironment environment, 
        IConfiguration configuration,
        AppDbContext context)
    {
        _environment = environment;
        _configuration = configuration;
        _context = context;
    }

    [HttpPost("profile-image")]
    public async Task<IActionResult> UploadProfileImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("File too large (max 5MB)");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            return BadRequest("Invalid file type");

        var fileName = $"{Guid.NewGuid()}{extension}";
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profile-images");

        Directory.CreateDirectory(uploadsFolder);

        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var imageUrl = $"{baseUrl}/uploads/profile-images/{fileName}";

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var user = await _context.Users
            .Include(u => u.UserData)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.UserData != null)
        {
            user.UserData.ProfileImage = imageUrl;
            await _context.SaveChangesAsync();
        }

        return Ok(new { url = imageUrl });
    }
}