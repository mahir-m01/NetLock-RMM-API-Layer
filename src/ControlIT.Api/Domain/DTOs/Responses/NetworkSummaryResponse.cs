namespace ControlIT.Api.Domain.DTOs.Responses;

public record NetworkSummaryResponse(
    int TotalPeers,
    int ConnectedPeers,
    int TenantPeers,
    int TenantConnectedPeers,
    int SetupKeysActive,
    int RouteCount);
