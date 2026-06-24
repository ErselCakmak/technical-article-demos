using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var endpoint = ReportEndpoint.FromConfiguration("https://reports.example.com/api/reports/submit");
var report = new CrashReport(
    Subject: "Unexpected application error",
    Body: "Safe public demo payload. Do not send private logs by default.",
    ApplicationVersion: "1.0.0-demo");

using var httpClient = new HttpClient();
var client = new CrashReportClient(httpClient, endpoint, apiKey: "demo-api-key");

HttpRequestMessage request = client.BuildRequest(report);

Console.WriteLine(request.Method);
Console.WriteLine(request.RequestUri);
Console.WriteLine("Request prepared. The demo does not send it to a real server.");

public sealed record CrashReport(string Subject, string Body, string ApplicationVersion);

public sealed class ReportEndpoint
{
    private ReportEndpoint(Uri uri) => Uri = uri;

    public Uri Uri { get; }

    public static ReportEndpoint FromConfiguration(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException("The report API URL must be an absolute URI.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Report delivery should use HTTPS.");
        }

        return new ReportEndpoint(uri);
    }
}

public sealed class CrashReportClient
{
    private readonly HttpClient _httpClient;
    private readonly ReportEndpoint _endpoint;
    private readonly string _apiKey;

    public CrashReportClient(HttpClient httpClient, ReportEndpoint endpoint, string apiKey)
    {
        _httpClient = httpClient;
        _endpoint = endpoint;
        _apiKey = apiKey;
    }

    public HttpRequestMessage BuildRequest(CrashReport report)
    {
        string json = JsonSerializer.Serialize(report);

        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint.Uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.UserAgent.ParseAdd("CrashReportBoundaryDemo/1.0");
        return request;
    }

    public async Task SendAsync(CrashReport report, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = BuildRequest(report);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
