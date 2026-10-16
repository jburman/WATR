using System.ComponentModel.DataAnnotations;

namespace WATR.Shared.Models;

public class UserPassKey
{
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }
    
    [Required]
    public byte[] CredentialId { get; set; } = Array.Empty<byte>();
    
    [Required]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();
    
    public uint SignatureCounter { get; set; }
    
    public Guid AaGuid { get; set; }
    
    // Navigation property
    public User? User { get; set; }
}
