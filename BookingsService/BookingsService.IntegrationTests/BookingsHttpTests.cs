using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BookingsService.IntegrationTests;

public sealed class BookingsHttpTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BookingsHttpTests(ApiFactory factory) => _factory = factory;

    private sealed record BookingDto(Guid Id, Guid EventId, string Status);

    private HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Token(Guid userId, string role = "User") => TestTokens.Create(userId, role);

    private static async Task<BookingDto> BookAsync(HttpClient client, Guid? eventId = null)
    {
        var response = await client.PostAsync($"/events/{eventId ?? Guid.NewGuid()}/book", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BookingDto>())!;
    }

    [Fact]
    public async Task Book_WithoutToken_Returns401()
    {
        var response = await Client().PostAsync($"/events/{Guid.NewGuid()}/book", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_WithGarbageToken_Returns401()
    {
        var response = await Client("not-a-jwt").GetAsync($"/bookings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_WithTamperedSignature_Returns401()
    {
        var token = Token(Guid.NewGuid());
        var index = token.LastIndexOf('.') + 5;
        var tampered = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];

        var response = await Client(tampered).GetAsync($"/bookings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Book_WithUserToken_Returns202_WithLocation_ForAnyEventId()
    {
        var eventId = Guid.NewGuid();

        var response = await Client(Token(Guid.NewGuid())).PostAsync($"/events/{eventId}/book", content: null);
        var body = await response.Content.ReadFromJsonAsync<BookingDto>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(eventId, body!.EventId);
        Assert.Equal($"/bookings/{body.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Book_WithEmptyEventId_Returns400()
    {
        var response = await Client(Token(Guid.NewGuid())).PostAsync($"/events/{Guid.Empty}/book", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_OfAnotherUser_Returns403_ForOwnerAndAdminReturns200()
    {
        var ownerToken = Token(Guid.NewGuid());
        var strangerToken = Token(Guid.NewGuid());
        var adminToken = Token(Guid.NewGuid(), "Admin");
        var booking = await BookAsync(Client(ownerToken));

        var asStranger = await Client(strangerToken).GetAsync($"/bookings/{booking.Id}");
        var asOwner = await Client(ownerToken).GetAsync($"/bookings/{booking.Id}");
        var asAdmin = await Client(adminToken).GetAsync($"/bookings/{booking.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, asStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    [Fact]
    public async Task GetBooking_Unknown_Returns404()
    {
        var response = await Client(Token(Guid.NewGuid())).GetAsync($"/bookings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_ByOwner_Returns204_AndStatusIsCancelled()
    {
        var ownerToken = Token(Guid.NewGuid());
        var booking = await BookAsync(Client(ownerToken));

        var cancel = await Client(ownerToken).DeleteAsync($"/bookings/{booking.Id}");
        var after = await Client(ownerToken).GetFromJsonAsync<BookingDto>($"/bookings/{booking.Id}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal("Cancelled", after!.Status);
    }

    [Fact]
    public async Task CancelBooking_ByStranger_Returns403()
    {
        var booking = await BookAsync(Client(Token(Guid.NewGuid())));

        var response = await Client(Token(Guid.NewGuid())).DeleteAsync($"/bookings/{booking.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_Twice_Returns400()
    {
        var ownerToken = Token(Guid.NewGuid());
        var booking = await BookAsync(Client(ownerToken));
        await Client(ownerToken).DeleteAsync($"/bookings/{booking.Id}");

        var second = await Client(ownerToken).DeleteAsync($"/bookings/{booking.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Book_EleventhActiveBooking_Returns409()
    {
        var client = Client(Token(Guid.NewGuid()));

        for (var i = 0; i < 10; i++)
            await BookAsync(client);

        var eleventh = await client.PostAsync($"/events/{Guid.NewGuid()}/book", content: null);

        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);
    }
}