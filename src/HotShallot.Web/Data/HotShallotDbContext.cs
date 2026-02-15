using Microsoft.EntityFrameworkCore;
using HotShallot.Shared.Models;

namespace HotShallot.Web.Data;

public class HotShallotDbContext : DbContext
{
    public HotShallotDbContext(DbContextOptions<HotShallotDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPassKey> UserPassKeys => Set<UserPassKey>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskNote> TaskNotes => Set<TaskNote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // UserPassKey configuration
        modelBuilder.Entity<UserPassKey>(entity =>
        {
            entity.HasOne(d => d.User)
                .WithMany(p => p.PassKeys)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            
            entity.HasOne(d => d.CreatedBy)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectMember configuration
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => new { e.ProjectId, e.UserId });

            entity.Property(e => e.Role)
                .HasConversion<string>();

            entity.HasOne(d => d.Project)
                .WithMany(p => p.Members)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.User)
                .WithMany(p => p.ProjectMemberships)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskItem configuration
        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasOne(d => d.Project)
                .WithMany(p => p.Tasks)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.AssignedTo)
                .WithMany()
                .HasForeignKey(d => d.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.CreatedBy)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.Status)
                .HasConversion<int>();

            entity.Property(e => e.Priority)
                .HasConversion<int>();
        });

        // TaskNote configuration
        modelBuilder.Entity<TaskNote>(entity =>
        {
            entity.HasOne(d => d.Task)
                .WithMany(p => p.Notes)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Author)
                .WithMany()
                .HasForeignKey(d => d.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
