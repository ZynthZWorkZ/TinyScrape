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
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 2000;

        public NetworkMonitor(IWebDriver driver)
        {
            _driver = driver;
        }

        public async Task MonitorNetworkTraffic(bool searchForM3u8 = false)
        {
            try
            {
                // Clear existing logs
                _driver.Manage().Logs.GetLog(LogType.Performance);

                // Wait for any network activity
                await Task.Delay(5000);

                // Get initial network requests
                await ProcessNetworkLogs(searchForM3u8);

                // Check for any iframes that might contain video players
                await CheckIframes(searchForM3u8);

                // Additional wait for any delayed requests
                await Task.Delay(5000);

                // Get final network requests
                await ProcessNetworkLogs(searchForM3u8);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error monitoring network traffic");
            }
        }

        private async Task ProcessNetworkLogs(bool searchForM3u8)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
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
                                if (methodStr == "Network.responseReceived" || methodStr == "Network.requestWillBeSent")
                                {
                                    if (message.TryGetProperty("params", out var params_) &&
                                        params_.TryGetProperty(methodStr == "Network.responseReceived" ? "response" : "request", out var response) &&
                                        response.TryGetProperty("url", out var url))
                                    {
                                        var urlString = url.GetString();
                                        if (urlString != null)
                                        {
                                            if (searchForM3u8 && urlString.Contains(".m3u8"))
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
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    break; // Success, exit retry loop
                }
                catch (Exception ex)
                {
                    if (retry == MaxRetries - 1)
                    {
                        Log.Error(ex, "Failed to process network logs after all retries");
                        throw;
                    }
                    await Task.Delay(RetryDelayMs);
                }
            }
        }

        private async Task CheckIframes(bool searchForM3u8)
        {
            var iframes = _driver.FindElements(By.TagName("iframe"));
            foreach (var iframe in iframes)
            {
                try
                {
                    _driver.SwitchTo().Frame(iframe);
                    await Task.Delay(2000);
                    await ProcessNetworkLogs(searchForM3u8);
                    _driver.SwitchTo().DefaultContent();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error checking iframe");
                    try
                    {
                        _driver.SwitchTo().DefaultContent();
                    }
                    catch
                    {
                        // Ignore errors when switching back to default content
                    }
                }
            }
        }

        public IEnumerable<string> GetM3u8Urls() => _m3u8Urls;
        public IEnumerable<string> GetCloudnestraUrls() => _cloudnestraUrls;
    }
} 