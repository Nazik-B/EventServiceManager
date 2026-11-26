using System.ComponentModel.DataAnnotations;

namespace EventsService.Application.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    [Range(1, int.MaxValue)]
    public int EventTtlSeconds { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int TopEventsTtlSeconds { get; set; } = 60;
}