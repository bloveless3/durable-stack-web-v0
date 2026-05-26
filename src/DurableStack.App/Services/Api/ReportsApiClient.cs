using System.Net.Http.Headers;
using System.Net.Http.Json;
using DurableStack.App.Configuration;
using DurableStack.Platform.Contracts;
using Microsoft.Extensions.Options;

namespace DurableStack.App.Services.Api;

public interface IReportsApiClient
{
    Task<ReportDashboardResponse?> GetDashboardAsync(ReportDashboardQueryRequest request, string bearerToken, string? correlationId, CancellationToken cancellationToken = default);
}

public sealed class ReportsApiClient : IReportsApiClient
{
    private readonly HttpClient _httpClient;

    public ReportsApiClient(HttpClient httpClient, IOptions<DurableStackApiOptions> apiOptions)
    {
        _httpClient = httpClient;

        var baseUrl = apiOptions.Value.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("DurableStackApi:BaseUrl is required.");
        }

        _httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    }

    public async Task<ReportDashboardResponse?> GetDashboardAsync(
        ReportDashboardQueryRequest request,
        string bearerToken,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/reports/dashboard/query")
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            httpRequest.Headers.Add("X-Correlation-Id", correlationId);
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ReportDashboardResponse>(cancellationToken: cancellationToken);
    }
}
