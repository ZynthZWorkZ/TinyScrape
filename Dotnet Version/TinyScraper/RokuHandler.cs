using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Serilog;

namespace TinyScraper
{
    public static class RokuHandler
    {
        public static async Task CreateRokuApp(string title, string videoUrl, string rokuIp)
        {
            try
            {
                // Create a new directory for the app
                string appDir = Path.Combine("RokuSideload", title.Replace(" ", "_"));
                if (!Directory.Exists(appDir))
                {
                    Directory.CreateDirectory(appDir);
                }

                // Copy all files from VideoPlay template
                string templateDir = Path.Combine("RokuSideload", "VideoPlay");
                foreach (var item in Directory.GetFileSystemEntries(templateDir))
                {
                    string dest = Path.Combine(appDir, Path.GetFileName(item));
                    if (Directory.Exists(item))
                    {
                        CopyDirectory(item, dest);
                    }
                    else
                    {
                        File.Copy(item, dest, true);
                    }
                }

                // Update videoscene.xml with title and URL
                string videoscenePath = Path.Combine(appDir, "components", "videoscene.xml");
                var doc = XDocument.Load(videoscenePath);
                var root = doc.Root;
                if (root != null)
                {
                    var titleElement = root.Descendants().FirstOrDefault(e => e.Value.Contains("videocontent.title"));
                    var urlElement = root.Descendants().FirstOrDefault(e => e.Value.Contains("videocontent.url"));

                    if (titleElement != null)
                    {
                        titleElement.Value = titleElement.Value.Replace("videocontent.title = \"\"", $"videocontent.title = \"{title}\"");
                    }
                    if (urlElement != null)
                    {
                        urlElement.Value = urlElement.Value.Replace("videocontent.url = \"\"", $"videocontent.url = \"{videoUrl}\"");
                    }

                    doc.Save(videoscenePath);
                }

                // Create zip file
                string zipPath = $"{appDir}.zip";
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                ZipFile.CreateFromDirectory(appDir, zipPath);

                // Upload to Roku
                if (await UploadToRoku(rokuIp, zipPath))
                {
                    Log.Information($"Successfully uploaded {title} to Roku device at {rokuIp}");
                }
                else
                {
                    Log.Error($"Failed to upload {title} to Roku device");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating Roku app");
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        private static async Task<bool> UploadToRoku(string ipAddress, string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Log.Error($"File not found: {filePath}");
                    return false;
                }

                var options = new ChromeOptions();
                options.AddArgument("--start-maximized");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--no-sandbox");

                using var driver = new ChromeDriver(options);
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

                string url = $"http://{ipAddress}/";
                driver.Navigate().GoToUrl(url);

                Log.Information("\nPlease log in to the Roku device in the browser window...");
                wait.Until(d => d.FindElement(By.TagName("frameset")));

                var frames = driver.FindElements(By.TagName("frame"));
                driver.SwitchTo().Frame(frames[0]);

                var fileInput = driver.FindElement(By.CssSelector("input.form-input-file[name='archive']"));
                fileInput.SendKeys(Path.GetFullPath(filePath));
                await Task.Delay(2000);

                ((IJavaScriptExecutor)driver).ExecuteScript(@"
                    var form = document.querySelector('form');
                    if (form) {
                        form.action = '/plugin_install';
                        form.method = 'POST';
                        form.enctype = 'multipart/form-data';
                        form.submit();
                    }
                ");

                await Task.Delay(15000);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error uploading to Roku");
                return false;
            }
        }
    }
} 