using CustomerGrpcServer.Models;
using Microsoft.EntityFrameworkCore;

namespace CustomerGrpcServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerEntity>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            e.Property(x => x.Email).HasColumnName("email").IsRequired().HasMaxLength(320);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.HasIndex(x => x.Email).IsUnique();
        });
    }
}
