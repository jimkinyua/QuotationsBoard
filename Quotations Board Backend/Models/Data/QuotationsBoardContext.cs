using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class QuotationsBoardContext : IdentityDbContext
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

}