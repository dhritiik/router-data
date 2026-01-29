using Microsoft.EntityFrameworkCore;

namespace QueryRouter.Data.SQL;

public class RequirementEntity
{
    public int Id { get; set; }
    public string ClientReferenceId { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Action
    public string ActionVerb { get; set; } = string.Empty;
    public string ActionModality { get; set; } = string.Empty;
    
    // Object
    public string ObjectPrimary { get; set; } = string.Empty;
    public string ObjectSecondary { get; set; } = string.Empty; // JSON array
    
    // Constraint
    public string ConstraintType { get; set; } = string.Empty;
    public string ConstraintDescription { get; set; } = string.Empty;
    public string ConstraintSubcategories { get; set; } = string.Empty; // JSON array
    
    // Classification
    public string RequirementType { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    
    // Entities
    public string Systems { get; set; } = string.Empty; // JSON array
    public string Standards { get; set; } = string.Empty; // JSON array
    public string Regulations { get; set; } = string.Empty; // JSON array
    public string Regions { get; set; } = string.Empty; // JSON array
    
    // Provenance
    public string? DocumentName { get; set; }
    public string? Section { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PosDbContext : DbContext
{
    public DbSet<RequirementEntity> Requirements { get; set; } = null!;

    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequirementEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.ClientReferenceId);
            entity.HasIndex(e => e.ConstraintType);
            entity.HasIndex(e => e.RequirementType);
            entity.HasIndex(e => e.Criticality);
            
            entity.Property(e => e.ClientReferenceId).IsRequired();
            entity.Property(e => e.NormalizedText).IsRequired();
            entity.Property(e => e.RawText).IsRequired();
        });
    }
}
