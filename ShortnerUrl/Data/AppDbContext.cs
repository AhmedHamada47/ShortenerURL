using Microsoft.EntityFrameworkCore;
using ShortnerUrl.Models;

namespace ShortnerUrl.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UrlShortener> UrlShorteners { get; set; }
        public DbSet<UrlClick> UrlClicks { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UrlShortener>(entity =>
            {
                entity.ToTable("UrlShorteners");
                entity.HasIndex(e => e.ShortCode).IsUnique();
                entity.HasMany(e => e.UrlClicks)
                      .WithOne(e => e.UrlShortener)
                      .HasForeignKey(e => e.UrlShortenerId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UrlClick>(entity =>
            {
                entity.ToTable("UrlClicks");
            });

            modelBuilder.Entity<ApiKey>(entity =>
            {
                entity.ToTable("ApiKeys");
                entity.HasIndex(e => e.KeyHash).IsUnique();
            });
        }
    }
}
