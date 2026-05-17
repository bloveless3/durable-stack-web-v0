using System.Net.Http.Json;
using DurableStack.Platform.Contracts;
using Microsoft.Extensions.Options;

namespace DurableStack.App.Services.Api;

public sealed class DurableStackApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DurableStackApiOptions _options;

    public DurableStackApiClient(HttpClient httpClient, IOptions<DurableStackApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ReportSummaryResponse?> GetReportSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/reports/summary");
        AddAuthHeaders(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ReportSummaryResponse>(cancellationToken: cancellationToken);
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.TenantId))
        {
            request.Headers.Add("X-DurableStack-TenantId", _options.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            request.Headers.Add("X-DurableStack-ClientSecret", _options.ClientSecret);
        }
    }
}
