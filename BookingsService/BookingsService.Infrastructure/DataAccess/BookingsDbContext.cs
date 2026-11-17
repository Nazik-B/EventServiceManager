using BookingsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingsService.Infrastructure.DataAccess;

public sealed class BookingsDbContext : DbContext
{
    public BookingsDbContext(DbContextOptions<BookingsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingsDbContext).Assembly);

        modelBuilder.Entity<Booking>()
            .Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }
}
