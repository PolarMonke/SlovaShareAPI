using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

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
        try
        {
            var result = await UploadFile(file, "profile-images");
            if (!result.Success)
                return BadRequest(result.Error);

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users
                .Include(u => u.UserData)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.UserData != null)
            {
                user.UserData.ProfileImage = result.Url;
                await _context.SaveChangesAsync();
            }

            return Ok(new { url = result.Url });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("story-cover")]
    public async Task<IActionResult> UploadStoryCover(IFormFile file)
    {
        try
        {
            var result = await UploadFile(file, "story-covers");
            if (!result.Success)
                return BadRequest(new { error = result.Error });

            return Ok(new { url = result.Url });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Internal server error: {ex.Message}" });
        }
    }

    private async Task<(bool Success, string Url, string Error)> UploadFile(IFormFile file, string subfolder)
    {
        if (file == null || file.Length == 0)
            return (false, null, "No file uploaded");

        if (file.Length > 5 * 1024 * 1024)
            return (false, null, "File too large (max 5MB)");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            return (false, null, "Invalid file type. Only images are allowed");

        var fileName = $"{Guid.NewGuid()}{extension}";
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subfolder);

        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var fileUrl = $"{baseUrl}/uploads/{subfolder}/{fileName}";

        return (true, fileUrl, null);
    }
}