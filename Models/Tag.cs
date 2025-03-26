using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Backend;

public class Tag
{
    public int Id { get; set; }
    [StringLength(1000)]
    public string? Name { get; set; }
}