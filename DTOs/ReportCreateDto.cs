using System.ComponentModel.DataAnnotations;

public class ReportCreateDto
{
    [Required, MaxLength(100)]
    public string? Reason { get; set; }
    
    [MaxLength(1000)]
    public string? Details { get; set; }
}