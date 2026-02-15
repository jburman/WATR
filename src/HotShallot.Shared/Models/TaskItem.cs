using System.ComponentModel.DataAnnotations;
using HotShallot.Shared.Enums;

namespace HotShallot.Shared.Models;

public class TaskItem
{
    public Guid Id { get; set; }
    
    public Guid ProjectId { get; set; }
    
    [Required]
    public string Title { get; set; } = string.Empty;
    
    public string Body { get; set; } = string.Empty;
    
    public TaskItemStatus Status { get; set; }
    
    public TaskPriority Priority { get; set; }
    
    public Guid? AssignedToUserId { get; set; }
    
    public Guid CreatedByUserId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public DateTime? ClosedAt { get; set; }
    
    // Navigation properties
    public Project? Project { get; set; }
    public User? AssignedTo { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<TaskNote> Notes { get; set; } = new List<TaskNote>();
}
