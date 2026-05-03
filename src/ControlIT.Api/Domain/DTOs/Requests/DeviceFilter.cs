using System.ComponentModel.DataAnnotations;

namespace ControlIT.Api.Domain.DTOs.Requests;

public class DeviceFilter
{
    public string? Platform { get; set; }

    public bool? OnlineOnly { get; set; }

    [StringLength(200)]
    public string? SearchTerm { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}
