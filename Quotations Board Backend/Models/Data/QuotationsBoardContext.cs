using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public class QuotationsBoardContext : IdentityDbContext<PortalUser>
{
    public QuotationsBoardContext(DbContextOptions<QuotationsBoardContext> options) : base(options)
    {
    }
    public QuotationsBoardContext()
    {
    }

    // On Configuring. Called when the DbContextOptions are not provided to the constructor
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
        optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var modifiedEntities = ChangeTracker.Entries()
       .Where(e => e.State == EntityState.Added
       || e.State == EntityState.Modified
       || e.State == EntityState.Deleted)
       .ToList();

        foreach (var modifiedEntity in modifiedEntities)
        {
            var AuditLogTosave = new AuditTrail
            {
                EntityName = modifiedEntity.Entity.GetType().Name,
                EntityId = modifiedEntity.Entity.ToString(),
                Action = modifiedEntity.State.ToString(),
                ActionBy = "System",
                ActionDate = DateTime.Now.ToString(),
                ActionDetails = GetChanges(modifiedEntity),
                InstitutionId = "System",
                ActionTime = DateTime.Now
            };
            AuditTrails.Add(AuditLogTosave);
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    // overide SaveChanges too
    public override int SaveChanges()
    {
        var modifiedEntities = ChangeTracker.Entries()
       .Where(e => e.State == EntityState.Added
       || e.State == EntityState.Modified
       || e.State == EntityState.Deleted)
       .ToList();

        foreach (var modifiedEntity in modifiedEntities)
        {
            var AuditLogTosave = new AuditTrail
            {
                EntityName = modifiedEntity.Entity.GetType().Name,
                EntityId = modifiedEntity.Entity.ToString(),
                Action = modifiedEntity.State.ToString(),
                ActionBy = "System",
                ActionDate = DateTime.Now.ToString(),
                ActionDetails = GetChanges(modifiedEntity),
                InstitutionId = "System",
                ActionTime = DateTime.Now
            };
            AuditTrails.Add(AuditLogTosave);
        }

        return base.SaveChanges();
    }

    private static string GetChanges(EntityEntry entity)
    {
        var changes = new StringBuilder();
        foreach (var property in entity.OriginalValues.Properties)
        {
            var originalValue = entity.OriginalValues[property];
            var currentValue = entity.CurrentValues[property];
            if (!Equals(originalValue, currentValue))
            {
                changes.AppendLine($"{property.Name}: From '{originalValue}' to '{currentValue}'");
            }
        }
        return changes.ToString();
    }

    public DbSet<InstitutionApplication> InstitutionApplications { get; set; } = null!;
    public DbSet<InstitutionType> InstitutionTypes { get; set; } = null!;
    public DbSet<Institution> Institutions { get; set; } = null!;
    public DbSet<Bond> Bonds { get; set; } = null!;
    public DbSet<Quotation> Quotations { get; set; } = null!;
    public DbSet<GorvermentBondTradeStage> GorvermentBondTradeStages { get; set; } = null!;
    public DbSet<GorvermentBondTradeLineStage> GorvermentBondTradeLinesStage { get; set; } = null!;
    public DbSet<BondTrade> BondTrades { get; set; } = null!;
    public DbSet<BondTradeLine> BondTradeLines { get; set; } = null!;
    public DbSet<ImpliedYield> ImpliedYields { get; set; } = null!;
    public DbSet<TBill> TBills { get; set; } = null!;
    public DbSet<QuotationEdit> QuotationEdits { get; set; } = null!;
    public DbSet<TBillYield> TBillYields { get; set; } = null!;
    public DbSet<Tenure> Tenures { get; set; } = null!;
    public DbSet<DraftImpliedYield> DraftImpliedYields { get; set; } = null!;
    public DbSet<TBillImpliedYield> TBillImpliedYields { get; set; } = null!;
    public DbSet<AuditTrail> AuditTrails { get; set; } = null!;

}