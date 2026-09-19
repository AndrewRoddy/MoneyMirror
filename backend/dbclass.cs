using Microsoft.EntityFrameworkCore;
using MoneyMirror.Data.Entities;

namespace MoneyMirror.Data;

public class MoneyMirrorDbContext : DbContext
{
    public MoneyMirrorDbContext(DbContextOptions<MoneyMirrorDbContext> options) : base(options) { }

    // Entities for database
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<Liability> Liabilities => Set<Liability>();
    public DbSet<PhysicalAsset> PhysicalAssets => Set<PhysicalAsset>();
    public DbSet<AssetValuationRecord> AssetValuationRecords => Set<AssetValuationRecord>();
    public DbSet<ProfessionalProfile> ProfessionalProfiles => Set<ProfessionalProfile>();
    public DbSet<EducationRecord> EducationRecords => Set<EducationRecord>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Experience> Experiences => Set<Experience>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfessionalProfile>().HasData(new ProfessionalProfile
        {
            Id = ProfessionalProfile.DefaultId,
            DisplayName = "Default Profile",
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });

        modelBuilder.Entity<FinancialAccount>().Property(account => account.CurrentBalance)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Liability>().Property(liability => liability.OutstandingBalance)
            .HasPrecision(18, 2);
        modelBuilder.Entity<PhysicalAsset>().Property(asset => asset.PurchasePrice)
            .HasPrecision(18, 2);
        modelBuilder.Entity<AssetValuationRecord>().Property(record => record.EstimatedValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PhysicalAsset>()
            .HasMany(asset => asset.ValuationRecords)
            .WithOne(record => record.PhysicalAsset)
            .HasForeignKey(record => record.PhysicalAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProfessionalProfile>()
            .HasMany(profile => profile.EducationRecords)
            .WithOne(record => record.ProfessionalProfile)
            .HasForeignKey(record => record.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProfessionalProfile>()
            .HasMany(profile => profile.Certifications)
            .WithOne(certification => certification.ProfessionalProfile)
            .HasForeignKey(certification => certification.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProfessionalProfile>()
            .HasMany(profile => profile.Skills)
            .WithOne(skill => skill.ProfessionalProfile)
            .HasForeignKey(skill => skill.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProfessionalProfile>()
            .HasMany(profile => profile.Experiences)
            .WithOne(experience => experience.ProfessionalProfile)
            .HasForeignKey(experience => experience.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
