using CloudFri.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ProjectContract> ProjectContracts => Set<ProjectContract>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ContractFile> ContractFiles => Set<ContractFile>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();
    public DbSet<DeliverableSet> DeliverableSets => Set<DeliverableSet>();
    public DbSet<ProjectDoc> ProjectDocs => Set<ProjectDoc>();
    public DbSet<GcfxParse> GcfxParses => Set<GcfxParse>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormRecord> FormRecords => Set<FormRecord>();
    public DbSet<LibraryDoc> LibraryDocs => Set<LibraryDoc>();
    public DbSet<DictItem> DictItems => Set<DictItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.UserName).IsUnique();
        b.Entity<Project>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Contract>().HasIndex(x => x.No).IsUnique();
        b.Entity<FormTemplate>().HasIndex(x => x.Key).IsUnique();
        b.Entity<ProjectContract>().HasIndex(x => new { x.ProjectId, x.ContractId }).IsUnique();
        b.Entity<ProjectMember>().HasIndex(x => new { x.ProjectId, x.MemberId }).IsUnique();

        b.Entity<Project>().Property(x => x.SubmitAmount).HasPrecision(18, 2);
        b.Entity<Project>().Property(x => x.ApprovedAmount).HasPrecision(18, 2);
        b.Entity<Project>().Property(x => x.Fee).HasPrecision(18, 2);
        b.Entity<ProjectMember>().Property(x => x.Share).HasPrecision(6, 2);
        b.Entity<Contract>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<Receipt>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<GcfxParse>().Property(x => x.TotalAmount).HasPrecision(18, 2);
    }
}
