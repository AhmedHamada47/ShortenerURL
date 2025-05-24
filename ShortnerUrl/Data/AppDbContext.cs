using Microsoft.EntityFrameworkCore;

namespace ShortnerUrl.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Models.UrlShrotner> UrlShrotner { get; set; }
    
    }
}
