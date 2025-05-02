using System.ComponentModel.DataAnnotations;
public class EmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}

public class VerifyCodeRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    
    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; }
}