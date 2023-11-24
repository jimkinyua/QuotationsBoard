using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.Development.json").Build();
        optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }

    public DbSet<InstitutionApplication> InstitutionApplications { get; set; } = null!;
    public DbSet<InstitutionType> InstitutionTypes { get; set; } = null!;
    public DbSet<Institution> Institutions { get; set; } = null!;
    public DbSet<InstitutionUser> InstitutionUsers { get; set; } = null!;
}