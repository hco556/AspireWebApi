using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;

namespace AspireWebApi.ApiService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WindowsAuthScrapingController : ControllerBase
    {
        /// <summary>
        /// Scrapes a website using Windows credentials
        /// </summary>
        /// <param name="url">The URL to scrape</param>
        /// <param name="domain">Windows domain (optional, uses current user if not provided)</param>
        /// <param name="username">Windows username</param>
        /// <param name="password">Windows password</param>
        [HttpPost("scrape")]
        public async Task<IActionResult> ScrapeWithCredentials([FromBody] ScrapeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Url))
                    return BadRequest("URL is required");

                // If username not provided, use current user
                string effectiveUsername = request.Username ?? GetCurrentUsername();
                string effectiveDomain = request.Domain ?? GetCurrentDomain();

                // Create secure password
                var securePassword = new SecureString();
                if (!string.IsNullOrEmpty(request.Password))
                {
                    foreach (char c in request.Password)
                        securePassword.AppendChar(c);
                }

                // Launch browser with user context
                var browser = await LaunchBrowserWithCredentials(
                    effectiveDomain,
                    effectiveUsername,
                    request.Password,
                    request.BrowserType ?? BrowserTypeClass.Chromium);

                if (browser == null)
                    return StatusCode(500, "Failed to launch browser with credentials");

                await using var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();

                // Navigate to the URL
                await page.GotoAsync(request.Url, new() { WaitUntil = WaitUntilState.NetworkIdle });

                // Extract page content
                var title = await page.TitleAsync();
                var url = page.Url;
                var content = await page.ContentAsync();

                await browser.CloseAsync();

                return Ok(new
                {
                    Success = true,
                    Message = "Successfully scraped website with Windows credentials",
                    PageTitle = title,
                    PageUrl = url,
                    ContentLength = content.Length,
                    Domain = effectiveDomain,
                    Username = effectiveUsername
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error scraping website",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Scrapes a website and extracts text content using Windows credentials
        /// </summary>
        [HttpPost("scrape-text")]
        public async Task<IActionResult> ScrapeTextWithCredentials([FromBody] ScrapeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Url))
                    return BadRequest("URL is required");

                string effectiveUsername = request.Username ?? GetCurrentUsername();
                string effectiveDomain = request.Domain ?? GetCurrentDomain();

                var securePassword = new SecureString();
                if (!string.IsNullOrEmpty(request.Password))
                {
                    foreach (char c in request.Password)
                        securePassword.AppendChar(c);
                }

                var browser = await LaunchBrowserWithCredentials(
                    effectiveDomain,
                    effectiveUsername,
                    request.Password,
                    request.BrowserType ?? BrowserTypeClass.Chromium);

                if (browser == null)
                    return StatusCode(500, "Failed to launch browser with credentials");

                await using var context = await browser.NewContextAsync();
                var page = await context.NewPageAsync();

                await page.GotoAsync(request.Url, new() { WaitUntil = WaitUntilState.NetworkIdle });

                var textContent = await page.TextContentAsync("body");
                var title = await page.TitleAsync();

                await browser.CloseAsync();

                return Ok(new
                {
                    Success = true,
                    PageTitle = title,
                    PageUrl = request.Url,
                    TextContent = textContent,
                    Domain = effectiveDomain,
                    Username = effectiveUsername
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = "Error scraping text content",
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Launches a browser with Windows credentials
        /// </summary>
        private async Task<IBrowser> LaunchBrowserWithCredentials(
            string domain,
            string username,
            string password,
            string browserType)
        {
            try
            {
                string browserExe = browserType == BrowserType.Firefox
                    ? @"C:\Program Files\Mozilla Firefox\firefox.exe"
                    //: browserType == BrowserType.WebKit
                    //    ? @"C:\Program Files\WebKit\bin\webkit.exe"
                        : @"C:\Program Files\Google\Chrome\Application\chrome.exe";

                var psi = new ProcessStartInfo
                {
                    FileName = browserExe,
                    Arguments = "--remote-debugging-port=9222 " +
                                "--no-first-run --no-default-browser-check " +
                                $"--user-data-dir=\"C:\\Temp\\UserProfile_{username}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                // Only set credentials if password is provided
                if (!string.IsNullOrEmpty(password))
                {
                    var securePassword = new SecureString();
                    foreach (char c in password)
                        securePassword.AppendChar(c);

                    psi.UserName = username;
                    psi.Password = securePassword;
                    psi.Domain = domain;
                    psi.LoadUserProfile = true;
                }

                Process.Start(psi);

                // Give browser time to start and open CDP port
                await Task.Delay(3000);

                using var playwright = await Playwright.CreateAsync();

                return browserType == BrowserType.Firefox
                    ? await playwright.Firefox.ConnectOverCDPAsync("http://localhost:9222")//, BrowserTypeConnectOverCDPOptions )
                    //: browserType == BrowserType.WebKit
                    //    ? await playwright.WebKit.ConnectOverCDPAsync("http://localhost:9222")
                        : await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching browser with credentials: {ex.Message}");
                return null;
            }
        }

        private string GetCurrentUsername()
        {
            try
            {
                string? fullName = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name;
                return fullName?.Split('\\').Last() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetCurrentDomain()
        {
            try
            {
                string? fullName = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name;
                var parts = fullName?.Split('\\');
                return parts?.Length == 2 ? parts[0] : Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }
    }

    public class ScrapeRequest
    {
        public string Url { get; set; }
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string BrowserType { get; set; }

        public ScrapeRequest()
        {
            BrowserType = BrowserTypeClass.Chromium;
        }
    }

    // Rename BrowserType static class to avoid name collision
    public static class BrowserTypeClass
    {
        public const string Chromium = "chromium";
        public const string Firefox = "firefox";
        public const string WebKit = "webkit";
    }
}
