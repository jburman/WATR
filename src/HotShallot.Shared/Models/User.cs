using System.ComponentModel.DataAnnotations;

namespace HotShallot.Shared.Models;

public class User
{
    public Guid Id { get; set; }
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<UserPassKey> PassKeys { get; set; } = new List<UserPassKey>();
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
}
