using System.ComponentModel.DataAnnotations;

namespace Backend;

public class UserData
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string? ProfileImage { get; set; }

    public UserData(int id)
    {
        Id = id;
        Description = null;
        ProfileImage = null;
    }
    public UserData() { }

}