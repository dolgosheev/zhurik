using Microsoft.Net.Http.Headers;

namespace Personal.Bot.Services;

public class GisMeteoService
{

    private readonly ILogger<GisMeteoService> _logger;
    private readonly string _host;
    private readonly string _token;
    private readonly IHttpClientFactory _httpClient;
    
    public GisMeteoService(IConfiguration config,ILogger<GisMeteoService> logger, IHttpClientFactory httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _host = config.GetValue<string>("GisMeteo:Host");
        _token = config.GetValue<string>("GisMeteo:Token");
    }

    public async Task GetWeatherAsync(string town)
    {
        var request = $"v2/search/cities/?lang=ru&query={town}";
        
        var httpRequestTimeout = TimeSpan.FromSeconds(15);
        
        using var httpClient = _httpClient.CreateClient();
        httpClient.Timeout = httpRequestTimeout;
        
        using var httpRequestMessage =
            new HttpRequestMessage(HttpMethod.Get, string.Format($@"{_host}/{request}"))
            {
                Headers =
                {
                    {HeaderNames.Accept, "application/json"},
                    {"X-Gismeteo-Token",_token}
                }
            };
        
        httpRequestMessage.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
        
        try
        {
            var httpResponseMessage = await httpClient
                .SendAsync(httpRequestMessage)
                .WaitAsync(httpRequestTimeout);

            if (httpResponseMessage.IsSuccessStatusCode)
                _logger.LogInformation("Responce {Responce}",httpResponseMessage);
            else
                _logger.LogWarning("Status code {Code}",httpResponseMessage.StatusCode);
        }
        catch (Exception e)
        {
            _logger.LogWarning("GisMeteo host is unavailable | Exception {Exception}", e.Message);
        }

        _logger.LogError("Request error");
        
        
    }
}