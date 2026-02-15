using System.ComponentModel.DataAnnotations;

namespace HotShallot.Shared.Models;

public class Project
{
    public Guid Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public Guid CreatedByUserId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }

    public List<string> TaskTypes { get; set; } = new();
    
    // Navigation properties
    public User? CreatedBy { get; set; }
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
