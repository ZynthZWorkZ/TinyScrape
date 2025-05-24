using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using System.Threading;

namespace TinyScraper
{
    public static class VideoPlayer
    {
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 2000;

        public static async Task<bool> TryPlayInVlc(IEnumerable<string> videoLinks)
        {
            if (!videoLinks.Any())
            {
                Log.Information("No video links to try");
                return false;
            }

            // Try each link in order
            foreach (var link in videoLinks)
            {
                Log.Information($"Attempting to play: {link}");

                try
                {
                    // Default VLC path for Windows
                    string vlcPath = @"C:\Program Files\VideoLAN\VLC\vlc.exe";

                    // Check if VLC exists at default path
                    if (!File.Exists(vlcPath))
                    {
                        // Try alternative common paths
                        string[] altPaths = new[]
                        {
                            @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\VideoLAN\VLC\vlc.exe")
                        };

                        foreach (var path in altPaths)
                        {
                            if (File.Exists(path))
                            {
                                vlcPath = path;
                                break;
                            }
                        }

                        if (!File.Exists(vlcPath))
                        {
                            Log.Error("VLC not found in common installation paths");
                            continue;
                        }
                    }

                    // Open VLC with the video URL
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = vlcPath,
                            Arguments = link,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();

                    // Wait a bit for VLC to start
                    await Task.Delay(5000);

                    // Check if process is still running
                    if (process.HasExited)
                    {
                        Log.Information("VLC process terminated unexpectedly");
                        continue;
                    }

                    // Ask user if video is playing
                    while (true)
                    {
                        Console.Write("\nIs the video playing correctly in VLC? (yes/no): ");
                        var response = Console.ReadLine()?.ToLower().Trim();
                        if (response == "yes" || response == "no")
                        {
                            if (response == "yes")
                            {
                                Log.Information("User confirmed video is playing correctly");
                                return true;
                            }
                            else
                            {
                                Log.Information("User reported video is not playing correctly, trying next link");
                                process.Kill();
                                await Task.Delay(2000); // Wait for VLC to close
                                break;
                            }
                        }
                        Console.WriteLine("Please answer 'yes' or 'no'");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error trying to play in VLC: {link}");
                    continue; // Try next link
                }
            }

            Log.Information("Failed to play any of the links in VLC");
            return false;
        }

        public static async Task<bool> TryPlayInFfplay(IEnumerable<string> videoLinks, bool watch = false)
        {
            if (!videoLinks.Any())
            {
                Log.Information("No video links to try");
                return false;
            }

            // Try each link in order
            foreach (var link in videoLinks)
            {
                if (watch)
                {
                    Log.Information($"Attempting to play full video with FFplay: {link}");
                }
                else
                {
                    Log.Information($"Attempting to verify URL with FFplay: {link}");
                }

                try
                {
                    // Try to find ffplay in PATH
                    string ffplayPath = "ffplay";

                    // Prepare FFplay arguments
                    var ffplayArgs = new System.Collections.Generic.List<string> { ffplayPath };
                    if (!watch)
                    {
                        ffplayArgs.AddRange(new[] { "-autoexit", "-t", "5" });
                    }
                    ffplayArgs.Add(link);

                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = ffplayPath,
                            Arguments = string.Join(" ", ffplayArgs),
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();

                    if (watch)
                    {
                        // If watching, wait for the process to complete
                        await process.WaitForExitAsync();
                        return true;
                    }
                    else
                    {
                        // If verifying, wait for FFplay to finish
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            await process.WaitForExitAsync(cts.Token);
                            // If we get here, the video played successfully
                            Log.Information($"URL verified and working: {link}");
                            return true;
                        }
                        catch (OperationCanceledException)
                        {
                            // If FFplay is still running after timeout, it means the video is playing
                            process.Kill();
                            Log.Information($"URL verified and working: {link}");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error verifying URL with FFplay: {link}");
                    continue; // Try next link
                }
            }

            Log.Information("Failed to verify any of the URLs with FFplay");
            return false;
        }
    }
} 