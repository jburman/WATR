using HotShallot.Shared.Enums;

namespace HotShallot.Shared.Models;

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    
    public Guid UserId { get; set; }
    
    public ProjectRole Role { get; set; }
    
    // Navigation properties
    public Project? Project { get; set; }
    public User? User { get; set; }
}
