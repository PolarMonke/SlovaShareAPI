using System.ComponentModel.DataAnnotations;

public class ProfileUpdateDto
{
    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? ProfileImage { get; set; }
}