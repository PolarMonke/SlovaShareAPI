using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class StoryTag
{
    [Required]
    public int StoryId { get; set; }

    [Required]
    public int TagId { get; set; }

    public Story? Story { get; set; }
    public Tag? Tag { get; set; }
}