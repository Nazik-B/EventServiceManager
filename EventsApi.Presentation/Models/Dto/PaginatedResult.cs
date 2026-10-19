namespace EventsApi.Models.Dto;

public class PaginatedResult<T>
{
    public int TotalCount { get; set; }
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
}