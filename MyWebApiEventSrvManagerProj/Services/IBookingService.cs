using EventsApi.Models;

namespace EventsApi.Services;

public interface IBookingService
{
    IEnumerable<Booking> GetAll();
    Booking? GetById(Guid id);
    Booking Create(Guid eventId);
    bool Delete(Guid id);
}