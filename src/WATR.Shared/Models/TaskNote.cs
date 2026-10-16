using System.ComponentModel.DataAnnotations;

namespace WATR.Shared.Models;

public class TaskNote
{
    public Guid Id { get; set; }
    
    public Guid TaskId { get; set; }
    
    public Guid AuthorUserId { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public TaskItem? Task { get; set; }
    public User? Author { get; set; }
}
