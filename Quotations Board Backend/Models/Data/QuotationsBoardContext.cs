using System.Security.Claims;
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
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var config = new ConfigurationBuilder()
       .SetBasePath(Directory.GetCurrentDirectory())
       .AddJsonFile("appsettings.json")
       .AddJsonFile($"appsettings.{environment}.json", optional: true)
       .AddEnvironmentVariables()
       .Build();

        optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
        // var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
        // optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var (UserId, InstitutionId) = GetUserInfo();
        LogAuditTrail(UserId, InstitutionId);
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        var (UserId, InstitutionId) = GetUserInfo();
        LogAuditTrail(UserId, InstitutionId);
        return base.SaveChanges();

    }

    private void LogAuditTrail(string UserId, string InstitutionId)
    {
        var modifiedEntities = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added
            || e.State == EntityState.Modified
            || e.State == EntityState.Deleted)
            .ToList();

        foreach (var modifiedEntity in modifiedEntities)
        {
            var auditLogToSave = new AuditTrail
            {
                EntityName = modifiedEntity.Entity.GetType().Name,
                EntityId = modifiedEntity.Entity.ToString(),
                Action = modifiedEntity.State.ToString(),
                ActionBy = UserId,
                ActionDate = DateTime.Now.ToString(),
                ActionDetails = GetChanges(modifiedEntity),
                InstitutionId = InstitutionId,
                ActionTime = DateTime.Now
            };
            AuditTrails.Add(auditLogToSave);
        }
    }

    private (string UserId, string InstitutionId) GetUserInfo()
    {
        var UserId = "Anonymous: User not logged in";
        var InstitutionId = "Anonymous: User not logged in hence not able to get Institution";
        var httpContextAccessor = new HttpContextAccessor();
        var user = httpContextAccessor.HttpContext?.User;
        if (user != null)
        {
            var claim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null)
            {
                UserId = claim.Value;
                var portalUser = Users.Include(x => x.Institution).FirstOrDefault(x => x.Id == UserId);
                if (portalUser != null)
                {
                    InstitutionId = portalUser.Institution.Id;
                }
            }
        }
        return (UserId, InstitutionId);
    }
    private static string GetChanges(EntityEntry entity)
    {
        var changes = new StringBuilder();
        var action = entity.State.ToString();

        changes.AppendLine($"Entity: {entity.Entity.GetType().Name}, Action: {action}");

        if (entity.State == EntityState.Modified)
        {
            foreach (var property in entity.OriginalValues.Properties)
            {
                var originalValue = entity.OriginalValues[property];
                var currentValue = entity.CurrentValues[property];
                if (!Equals(originalValue, currentValue))
                {
                    changes.AppendLine($"Property: {property.Name}, From: '{originalValue}', To: '{currentValue}'");
                }
            }
        }
        else if (entity.State == EntityState.Added)
        {
            foreach (var property in entity.CurrentValues.Properties)
            {
                var currentValue = entity.CurrentValues[property];
                changes.AppendLine($"Property: {property.Name}, Value: '{currentValue}'");
            }
        }
        else if (entity.State == EntityState.Deleted)
        {
            foreach (var property in entity.OriginalValues.Properties)
            {
                var originalValue = entity.OriginalValues[property];
                changes.AppendLine($"Property: {property.Name}, Original Value: '{originalValue}'");
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