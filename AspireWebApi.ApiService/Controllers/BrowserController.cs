using Microsoft.AspNetCore.Mvc;

namespace AspireWebApi.ApiService.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using System.Diagnostics;
    using System.Security;
    using Microsoft.Playwright;

    namespace YourNamespace.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class BrowserController : ControllerBase
        {
            [HttpPost("start-and-connect")]
            public async Task<IActionResult> StartAndConnect()
            {
                // -----------------------------
                // 1. Configure Windows user
                // -----------------------------
                string domain = "YOURDOMAIN";      // or machine name
                string username = "TargetUser";
                string password = "UserPassword";

                var securePassword = new SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);

                // -----------------------------
                // 2. Start Chrome or Edge
                // -----------------------------
                // Choose one:
                string browserExe = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
                // string browserExe = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";

                var psi = new ProcessStartInfo
                {
                    FileName = browserExe,
                    Arguments = "--remote-debugging-port=9222 --user-data-dir=\"C:\\Temp\\UserBProfile\"",
                    UserName = username,
                    Password = securePassword,
                    Domain = domain,
                    UseShellExecute = false,
                    LoadUserProfile = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Process.Start(psi);

                // Give the browser a moment to start
                await Task.Delay(3000);

                // -----------------------------
                // 3. Connect using Playwright
                // -----------------------------
                using var playwright = await Playwright.CreateAsync();

                var browser = await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");

                var contexts = browser.Contexts;
                var pages = contexts.SelectMany(c => c.Pages).ToList();
                pages.ForEach(p => Console.WriteLine($"Page URL: {p.Url}"));
                return Ok(new
                {
                    Message = "Connected to existing browser",
                    ContextCount = contexts.Count,
                    PageCount = pages.Count
                });
            }
        }
    }

}
