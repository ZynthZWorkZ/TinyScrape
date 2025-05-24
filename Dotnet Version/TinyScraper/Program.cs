using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml.Linq;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace TinyScraper
{
    public class Program
    {
        // Static fields to store state
        private static HashSet<string> m3u8Urls = new HashSet<string>();
        private static readonly string LogFile = "tinyzone.log";

        static async Task Main(string[] args)
        {
            // Set up logging
            SetupLogging();

            try
            {
                // Parse command line arguments
                var parser = new ArgumentParser();
                var arguments = parser.Parse(args);

                if (arguments.Search != null)
                {
                    await ProcessMovieLinks(arguments.Search, arguments.Vlc, arguments.Ffplay, arguments.Watch, !arguments.Head, arguments.RandomWatch, arguments.RokuSideload);
                }
                else if (arguments.Url != null)
                {
                    if (arguments.RokuSideload != null)
                    {
                        // Get movie details and video URL
                        await CheckPlayIcon(arguments.Url, false, false, false, !arguments.Head);
                        
                        if (m3u8Urls.Any())
                        {
                            string videoUrl = m3u8Urls.First();
                            string title = arguments.Url.Split('/').Reverse().Skip(1).First().Replace("-", " ").ToTitleCase();
                            await RokuHandler.CreateRokuApp(title, videoUrl, arguments.RokuSideload);
                        }
                    }
                    else
                    {
                        await CheckPlayIcon(arguments.Url, arguments.Vlc, arguments.Ffplay, arguments.Watch, !arguments.Head);
                    }
                }
                else
                {
                    await ProcessMovieLinks(null, arguments.Vlc, arguments.Ffplay, arguments.Watch, !arguments.Head, arguments.RandomWatch, arguments.RokuSideload);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while running the application");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void SetupLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(LogFile, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        private static async Task CheckPlayIcon(string url, bool tryVlc, bool tryFfplay, bool watchFfplay, bool headless)
        {
            var options = new ChromeOptions();
            if (headless)
            {
                options.AddArgument("--headless=new");
            }
            
            // Add all the Chrome options from the Python script
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=IsolateOrigins,site-per-process");
            options.AddArgument("--disable-site-isolation-trials");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--allow-insecure-localhost");
            options.AddArgument("--disable-webgl");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-notifications");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--disable-logging");
            options.AddArgument("--disable-default-apps");
            options.AddArgument("--disable-translate");
            options.AddArgument("--disable-sync");
            options.AddArgument("--disable-background-networking");
            options.AddArgument("--disable-background-timer-throttling");
            options.AddArgument("--disable-backgrounding-occluded-windows");
            options.AddArgument("--disable-breakpad");
            options.AddArgument("--disable-component-extensions-with-background-pages");
            options.AddArgument("--disable-dev-tools");

            // Add experimental options
            options.AddExcludedArgument("enable-automation");
            options.AddExcludedArgument("enable-logging");
            options.AddAdditionalOption("useAutomationExtension", false);

            // Enable performance logging
            options.SetLoggingPreference(LogType.Performance, LogLevel.All);
            options.SetLoggingPreference(LogType.Browser, LogLevel.All);
            options.SetLoggingPreference(LogType.Driver, LogLevel.All);

            // Create a temporary directory for Chrome user data
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            options.AddArgument($"--user-data-dir={tempDir}");

            using (var driver = new ChromeDriver(options))
            {
                try
                {
                    // Execute CDP commands to prevent detection
                    var cdpParams = new Dictionary<string, object>
                    {
                        ["userAgent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
                        ["platform"] = "Windows",
                        ["acceptLanguage"] = "en-US,en;q=0.9"
                    };
                    ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
                    ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });");
                    ((IJavaScriptExecutor)driver).ExecuteScript("Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });");
                    ((IJavaScriptExecutor)driver).ExecuteScript("window.chrome = { runtime: {} };");

                    // Navigate to the URL
                    driver.Navigate().GoToUrl(url);
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

                    // Try to find the play icon using different possible selectors
                    string[] selectors = new[] { "i.fas.fa-play", "i[class*='fa-play']", "i[class*='play']" };
                    IWebElement? playElement = null;

                    foreach (var selector in selectors)
                    {
                        try
                        {
                            playElement = wait.Until(d => d.FindElement(By.CssSelector(selector)));
                            if (playElement != null)
                            {
                                Log.Information($"Play icon found using selector: {selector}");
                                break;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (playElement != null)
                    {
                        // Wait a bit to ensure the page is fully loaded
                        await Task.Delay(2000);

                        try
                        {
                            // Scroll the element into view
                            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", playElement);
                            await Task.Delay(1000);

                            // Try different click methods
                            bool clicked = false;
                            try
                            {
                                playElement.Click();
                                Log.Information("Successfully clicked using direct click");
                                clicked = true;
                            }
                            catch
                            {
                                try
                                {
                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", playElement);
                                    Log.Information("Successfully clicked using JavaScript");
                                    clicked = true;
                                }
                                catch
                                {
                                    try
                                    {
                                        new Actions(driver).MoveToElement(playElement).Click().Perform();
                                        Log.Information("Successfully clicked using Action Chains");
                                        clicked = true;
                                    }
                                    catch
                                    {
                                        Log.Error("Failed to click using any method");
                                    }
                                }
                            }

                            if (clicked)
                            {
                                // Monitor network traffic for cloudnestra URLs only
                                var networkMonitor = new NetworkMonitor(driver);
                                await networkMonitor.MonitorNetworkTraffic(searchForM3u8: false);

                                // Get cloudnestra URLs
                                var cloudnestraUrls = networkMonitor.GetCloudnestraUrls().ToList();
                                if (cloudnestraUrls.Any())
                                {
                                    Log.Information("\nFound cloudnestra URLs:");
                                    var firstUrl = cloudnestraUrls.First();
                                    Log.Information($"Visiting first cloudnestra URL: {firstUrl}");

                                    // Navigate to the cloudnestra URL
                                    driver.Navigate().GoToUrl(firstUrl);
                                    await Task.Delay(15000); // Wait for page load like Python code

                                    // Wait for the play button
                                    var cloudnestraWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                                    try
                                    {
                                        var playButton = cloudnestraWait.Until(d => d.FindElement(By.CssSelector("#pl_but.fas.fa-play")));
                                        Log.Information("Found play button on cloudnestra page!");

                                        // Try to click the play button
                                        try
                                        {
                                            // Scroll the element into view
                                            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", playButton);
                                            await Task.Delay(1000);

                                            // Try different click methods
                                            bool cloudnestraClicked = false;
                                            try
                                            {
                                                playButton.Click();
                                                Log.Information("Successfully clicked play button using direct click");
                                                cloudnestraClicked = true;
                                            }
                                            catch
                                            {
                                                try
                                                {
                                                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", playButton);
                                                    Log.Information("Successfully clicked play button using JavaScript");
                                                    cloudnestraClicked = true;
                                                }
                                                catch
                                                {
                                                    try
                                                    {
                                                        new Actions(driver).MoveToElement(playButton).Click().Perform();
                                                        Log.Information("Successfully clicked play button using Action Chains");
                                                        cloudnestraClicked = true;
                                                    }
                                                    catch
                                                    {
                                                        Log.Error("Failed to click play button using any method");
                                                    }
                                                }
                                            }

                                            if (cloudnestraClicked)
                                            {
                                                // Wait for any new requests after clicking
                                                await Task.Delay(5000);

                                                // Now monitor network traffic for m3u8 URLs
                                                await networkMonitor.MonitorNetworkTraffic(searchForM3u8: true);

                                                // Get m3u8 URLs
                                                var m3u8Urls = networkMonitor.GetM3u8Urls().ToList();
                                                if (m3u8Urls.Any())
                                                {
                                                    Log.Information("\nFound m3u8 URLs:");
                                                    foreach (var m3u8Url in m3u8Urls)
                                                    {
                                                        Log.Information($"- {m3u8Url}");
                                                    }

                                                    // Store m3u8 URLs
                                                    Program.m3u8Urls.UnionWith(new HashSet<string>(m3u8Urls));

                                                    // Try playing in VLC if requested
                                                    if (tryVlc)
                                                    {
                                                        await VideoPlayer.TryPlayInVlc(m3u8Urls);
                                                    }

                                                    // Try playing in FFplay if requested
                                                    if (tryFfplay)
                                                    {
                                                        await VideoPlayer.TryPlayInFfplay(m3u8Urls, watchFfplay);
                                                    }
                                                }
                                                else
                                                {
                                                    Log.Information("No m3u8 URLs found after clicking play button");
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to click play button");
                                        }
                                    }
                                    catch
                                    {
                                        Log.Information("No play button found on cloudnestra page");
                                    }
                                }
                                else
                                {
                                    Log.Information("No cloudnestra URLs found in the network traffic");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to click the play icon");
                        }
                    }
                    else
                    {
                        Log.Information("Play icon not found on the page");
                    }
                }
                finally
                {
                    driver.Quit();
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        private static async Task ProcessMovieLinks(string? searchTerm, bool tryVlc, bool tryFfplay, bool watchFfplay, bool headless, bool randomWatch, string? rokuSideload)
        {
            try
            {
                // Read movie_links.txt
                var movies = File.ReadAllLines("movie_links.txt")
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();

                // Filter movies based on search term if provided
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    movies = movies.Where(movie => movie.ToLower().Contains(searchTerm)).ToList();
                }

                if (!movies.Any())
                {
                    Log.Information("No movies found matching the search criteria");
                    return;
                }

                if (randomWatch)
                {
                    // Select a random movie
                    var selectedMovie = movies[new Random().Next(movies.Count)];
                    var parts = selectedMovie.Split('|');
                    if (parts.Length >= 3)
                    {
                        var year = parts[0].Trim();
                        var title = parts[1].Trim();
                        var url = parts[2].Trim();

                        Log.Information($"\nRandomly selected movie: {year} | {title}");
                        Log.Information($"URL: {url}");

                        if (!string.IsNullOrEmpty(rokuSideload))
                        {
                            // Process the movie URL for Roku sideloading
                            await CheckPlayIcon(url, false, false, false, !headless);
                            if (m3u8Urls.Any())
                            {
                                var videoUrl = m3u8Urls.First();
                                await RokuHandler.CreateRokuApp(title, videoUrl, rokuSideload);
                            }
                        }
                        else
                        {
                            // Process the movie URL with VLC
                            await CheckPlayIcon(url, true, false, false, headless);
                        }
                        return;
                    }
                }

                Log.Information($"Found {movies.Count} movies matching your search:");

                // Display numbered list of movies
                for (int i = 0; i < movies.Count; i++)
                {
                    var parts = movies[i].Split('|');
                    if (parts.Length >= 3)
                    {
                        var year = parts[0].Trim();
                        var title = parts[1].Trim();
                        Console.WriteLine($"{i + 1}. {year} | {title}");
                    }
                }

                // Get user selection
                while (true)
                {
                    Console.Write("\nEnter the number of the movie to process (or 'q' to quit): ");
                    var selection = Console.ReadLine()?.ToLower().Trim();
                    if (selection == "q")
                    {
                        return;
                    }

                    if (int.TryParse(selection, out int index) && index > 0 && index <= movies.Count)
                    {
                        var selectedMovie = movies[index - 1];
                        var parts = selectedMovie.Split('|');
                        if (parts.Length >= 3)
                        {
                            var year = parts[0].Trim();
                            var title = parts[1].Trim();
                            var url = parts[2].Trim();

                            Log.Information($"\nProcessing: {year} | {title}");
                            Log.Information($"URL: {url}");

                            // Process the movie URL
                            await CheckPlayIcon(url, tryVlc, tryFfplay, watchFfplay, headless);
                            break;
                        }
                    }
                    Console.WriteLine("Invalid selection. Please try again.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading movie_links.txt");
            }
        }
    }

    public class ArgumentParser
    {
        public string? Url { get; private set; }
        public bool Vlc { get; private set; }
        public bool Ffplay { get; private set; }
        public bool Watch { get; private set; }
        public bool Head { get; private set; }
        public bool RandomWatch { get; private set; }
        public string? Search { get; private set; }
        public string? RokuSideload { get; private set; }

        public ArgumentParser Parse(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-vlc":
                    case "--vlc":
                        Vlc = true;
                        break;
                    case "-ffplay":
                    case "--ffplay":
                        Ffplay = true;
                        break;
                    case "-w":
                    case "--w":
                        Watch = true;
                        break;
                    case "-head":
                    case "--head":
                        Head = true;
                        break;
                    case "-rw":
                    case "--rw":
                        RandomWatch = true;
                        break;
                    case "-s":
                    case "--search":
                        if (i + 1 < args.Length)
                            Search = args[++i];
                        break;
                    case "-rokusl":
                    case "--rokusl":
                        if (i + 1 < args.Length)
                            RokuSideload = args[++i];
                        break;
                    default:
                        if (!args[i].StartsWith("-"))
                            Url = args[i];
                        break;
                }
            }
            return this;
        }
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var words = str.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }
    }
}
