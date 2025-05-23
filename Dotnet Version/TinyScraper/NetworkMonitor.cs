using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace TinyScraper
{
    public class NetworkMonitor
    {
        private readonly IWebDriver _driver;
        private readonly HashSet<string> _m3u8Urls = new();
        private readonly HashSet<string> _cloudnestraUrls = new();

        public NetworkMonitor(IWebDriver driver)
        {
            _driver = driver;
        }

        public async Task MonitorNetworkTraffic()
        {
            try
            {
                // Clear existing logs
                _driver.Manage().Logs.GetLog(LogType.Performance);

                // Wait for any network activity
                await Task.Delay(5000);

                // Get network requests
                var logs = _driver.Manage().Logs.GetLog(LogType.Performance);
                foreach (var entry in logs)
                {
                    try
                    {
                        var log = JsonSerializer.Deserialize<JsonElement>(entry.Message);
                        if (log.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("method", out var method))
                        {
                            var methodStr = method.GetString();
                            if (methodStr == "Network.responseReceived")
                            {
                                if (message.TryGetProperty("params", out var params_) &&
                                    params_.TryGetProperty("response", out var response) &&
                                    response.TryGetProperty("url", out var url))
                                {
                                    var urlString = url.GetString();
                                    if (urlString != null)
                                    {
                                        if (urlString.Contains(".m3u8"))
                                        {
                                            _m3u8Urls.Add(urlString);
                                            Log.Information($"Found m3u8 URL: {urlString}");
                                        }
                                        if (urlString.Contains("cloudnestra.com") && urlString.Contains("/rcp/"))
                                        {
                                            _cloudnestraUrls.Add(urlString);
                                            Log.Information($"Found cloudnestra scriptlet URL: {urlString}");
                                        }
                                    }
                                }
                            }
                            else if (methodStr == "Network.requestWillBeSent")
                            {
                                if (message.TryGetProperty("params", out var params_) &&
                                    params_.TryGetProperty("request", out var request) &&
                                    request.TryGetProperty("url", out var url))
                                {
                                    var urlString = url.GetString();
                                    if (urlString != null)
                                    {
                                        if (urlString.Contains(".m3u8"))
                                        {
                                            _m3u8Urls.Add(urlString);
                                            Log.Information($"Found m3u8 URL in request: {urlString}");
                                        }
                                        if (urlString.Contains("cloudnestra.com") && urlString.Contains("/rcp/"))
                                        {
                                            _cloudnestraUrls.Add(urlString);
                                            Log.Information($"Found cloudnestra scriptlet URL in request: {urlString}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Check for any iframes that might contain video players
                var iframes = _driver.FindElements(By.TagName("iframe"));
                foreach (var iframe in iframes)
                {
                    try
                    {
                        _driver.SwitchTo().Frame(iframe);
                        await Task.Delay(2000);

                        // Get network requests from iframe
                        var iframeLogs = _driver.Manage().Logs.GetLog(LogType.Performance);
                        foreach (var entry in iframeLogs)
                        {
                            try
                            {
                                var log = JsonSerializer.Deserialize<JsonElement>(entry.Message);
                                if (log.TryGetProperty("message", out var message) &&
                                    message.TryGetProperty("method", out var method))
                                {
                                    var methodStr = method.GetString();
                                    if (methodStr == "Network.responseReceived")
                                    {
                                        if (message.TryGetProperty("params", out var params_) &&
                                            params_.TryGetProperty("response", out var response) &&
                                            response.TryGetProperty("url", out var url))
                                        {
                                            var urlString = url.GetString();
                                            if (urlString != null)
                                            {
                                                if (urlString.Contains(".m3u8"))
                                                {
                                                    _m3u8Urls.Add(urlString);
                                                    Log.Information($"Found m3u8 URL in iframe: {urlString}");
                                                }
                                                if (urlString.Contains("cloudnestra.com") && urlString.Contains("/rcp/"))
                                                {
                                                    _cloudnestraUrls.Add(urlString);
                                                    Log.Information($"Found cloudnestra scriptlet URL in iframe: {urlString}");
                                                }
                                            }
                                        }
                                    }
                                    else if (methodStr == "Network.requestWillBeSent")
                                    {
                                        if (message.TryGetProperty("params", out var params_) &&
                                            params_.TryGetProperty("request", out var request) &&
                                            request.TryGetProperty("url", out var url))
                                        {
                                            var urlString = url.GetString();
                                            if (urlString != null)
                                            {
                                                if (urlString.Contains(".m3u8"))
                                                {
                                                    _m3u8Urls.Add(urlString);
                                                    Log.Information($"Found m3u8 URL in iframe request: {urlString}");
                                                }
                                                if (urlString.Contains("cloudnestra.com") && urlString.Contains("/rcp/"))
                                                {
                                                    _cloudnestraUrls.Add(urlString);
                                                    Log.Information($"Found cloudnestra scriptlet URL in iframe request: {urlString}");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        _driver.SwitchTo().DefaultContent();
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Additional wait for any delayed requests
                await Task.Delay(5000);

                // Get final network requests
                var finalLogs = _driver.Manage().Logs.GetLog(LogType.Performance);
                foreach (var entry in finalLogs)
                {
                    try
                    {
                        var log = JsonSerializer.Deserialize<JsonElement>(entry.Message);
                        if (log.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("method", out var method))
                        {
                            var methodStr = method.GetString();
                            if (methodStr == "Network.responseReceived")
                            {
                                if (message.TryGetProperty("params", out var params_) &&
                                    params_.TryGetProperty("response", out var response) &&
                                    response.TryGetProperty("url", out var url))
                                {
                                    var urlString = url.GetString();
                                    if (urlString != null)
                                    {
                                        if (urlString.Contains(".m3u8"))
                                        {
                                            _m3u8Urls.Add(urlString);
                                            Log.Information($"Found m3u8 URL: {urlString}");
                                        }
                                        if (urlString.Contains("cloudnestra.com") && urlString.Contains("/rcp/"))
                                        {
                                            _cloudnestraUrls.Add(urlString);
                                            Log.Information($"Found cloudnestra scriptlet URL: {urlString}");
                                        }
                                    }
                                }
                            }
                            else if (methodStr == "Network.requestWillBeSent")
                            {
                                if (message.TryGetProperty("params", out var params_) &&
                                    params_.TryGetProperty("request", out var request) &&
                                    request.TryGetProperty("url", out var url))
                                {
                                    var urlString = url.GetString();
                                    if (urlString != null)
                                    {
                                        if (urlString.Contains(".m3u8"))
                                        {
                                            _m3u8Urls.Add(urlString);
                                            Log.Information($"Found m3u8 URL in request: {urlString}");
                                        }
                                        if (urlString.Contains("cloudnestra.com") && urlString.Contains("/rcp/"))
                                        {
                                            _cloudnestraUrls.Add(urlString);
                                            Log.Information($"Found cloudnestra scriptlet URL in request: {urlString}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error monitoring network traffic");
            }
        }

        public IEnumerable<string> GetM3u8Urls() => _m3u8Urls;
        public IEnumerable<string> GetCloudnestraUrls() => _cloudnestraUrls;
    }
} 