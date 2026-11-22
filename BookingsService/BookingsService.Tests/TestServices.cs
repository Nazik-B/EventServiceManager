using BookingsService.Application;
using BookingsService.Application.Abstractions;
using BookingsService.Application.Repositories;
using BookingsService.Infrastructure.DataAccess;
using BookingsService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingsService.Tests;

internal static class TestServices
{
    public static ServiceProvider Create(IBookingEventPublisher? publisher = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<BookingsDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<IBookingEventPublisher>(publisher ?? new FakeBookingEventPublisher());
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
