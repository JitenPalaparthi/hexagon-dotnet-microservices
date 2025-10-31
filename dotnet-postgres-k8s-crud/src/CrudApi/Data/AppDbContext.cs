using Microsoft.EntityFrameworkCore;
using CrudApi.Models;

namespace CrudApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Item> Items => Set<Item>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Item>(e =>
            {
                e.HasKey(i => i.Id);
                e.Property(i => i.Name).IsRequired().HasMaxLength(200);
                e.Property(i => i.Price).HasPrecision(18,2);
            });
        }
    }
}
