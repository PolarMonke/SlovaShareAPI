using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

[ApiController]
[Route("[controller]")]
public class ConfirmationController : ControllerBase
{
    private readonly EmailService _emailService;
    private readonly IMemoryCache _memoryCache;

    public ConfirmationController(EmailService emailService, IMemoryCache memoryCache)
    {
        _emailService = emailService;
        _memoryCache = memoryCache;
    }

    [HttpPost("send-code")]
    public async Task<IActionResult> SendConfirmationCode([FromBody] EmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var code = new Random().Next(100000, 999999).ToString();
        
            _memoryCache.Set(request.Email, code, TimeSpan.FromMinutes(5));

            var emailBody = $"Your confirmation code is: {code}";
            await _emailService.SendEmailAsync(request.Email, "Confirmation Code", emailBody);

            return Ok(new { 
                Message = "Confirmation code sent successfully",
                Code = "register.codeSent"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                Message = "Failed to send confirmation code",
                Code = "register.sendCodeFailed",
                Error = ex.Message
            });
        }
    }

    [HttpPost("verify-code")]
    public IActionResult VerifyConfirmationCode([FromBody] VerifyCodeRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!_memoryCache.TryGetValue(request.Email, out string storedCode))
        {
            return BadRequest("Code expired or not found");
        }

        if (storedCode != request.Code)
        {
            return BadRequest("Invalid code");
        }

        _memoryCache.Remove(request.Email);

        return Ok(new { Message = "Code verified successfully" });
    }
}
