using Microsoft.AspNetCore.Mvc;

namespace AspireWebApi.ApiService.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Playwright;
    using System;
    using System.Diagnostics;
    using System.Security;
    using System.Security.Principal;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    namespace YourNamespace.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class BrowserController : ControllerBase
        {
            [HttpGet]
            public async Task<IActionResult> Get()
            {
                string? currentUser = WindowsIdentity.GetCurrent()?.Name;
                // -----------------------------
                // 1. Configure Windows user
                // -----------------------------
                //string domain = "YOURDOMAIN";      // or machine name
                //string username = "TargetUser";
                //string password = "UserPassword";

                //var securePassword = new SecureString();
                //foreach (char c in password)
                //    securePassword.AppendChar(c);

                // -----------------------------
                // 2. Start Chrome or Edge
                // -----------------------------
                // Choose one:
                string browserExe = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
                // string browserExe = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";

                var psi = new ProcessStartInfo
                {
                    FileName = browserExe,
                    //Arguments = "--remote-debugging-port=9222 --user-data-dir=\"C:\\Temp\\UserBProfile\"",
                    //Arguments = "https://www.google.be", // <-- navigate immediately
                    Arguments = "--remote-debugging-port=9222 " + "--no-first-run --no-default-browser-check " + "--user-data-dir=\"C:\\Temp\\UserProfileX\" " + "https://www.google.be",
                    //UserName = username,
                    //Password = securePassword,
                    //Domain = domain,
                    UseShellExecute = false,
                    //LoadUserProfile = true,
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

                var browser = await playwright.Chromium.ConnectOverCDPAsync("https://www.google.be");

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
            private string? GetCurrentUser(string userName)
            {
                 string? username = WindowsIdentity.GetCurrent().Name.Split('\\')[1];

                //if (!String.IsNullOrEmpty(userName))
                    

                return username;
            }
            [HttpPost]
            public async Task<IActionResult> Post()
            {


                using var playwright = await Playwright.CreateAsync();
                await using var browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = false,
                });
                var context = await browser.NewContextAsync();

                var page = await context.NewPageAsync();
                await page.GotoAsync("https://mudextensions.codebeam.org/mudselectextended");
                await page.Locator(".mud-input-slot").First.ClickAsync();
                await page.Locator("#listitem_aa0ff837").ClickAsync();
                await page.Locator("#selectextbgjja7fz > .mud-input-control > .mud-input-control-input-container > .mud-input.mud-input-text > .d-flex > div").First.ClickAsync();
                await page.Locator("#listitem_286bf343").ClickAsync();
                await page.Locator("#selectext08o8k0uo > .mud-input-control > .mud-input-control-input-container > .mud-input.mud-input-outlined > .mud-input-adornment > .mud-icon-root").ClickAsync();
                await page.Locator("#mudinputoebllmd5").ClickAsync();
                await page.Locator("#mudinputoebllmd5").FillAsync("arizona");
                await page.Locator("#mudinputoebllmd5").PressAsync("Enter");
                await page.Locator("#selectext08o8k0uo > .mud-input-control > .mud-input-control-input-container > .mud-input.mud-input-outlined > .d-flex > div").First.ClickAsync();
                await page.Locator("#listitem_55fc279f").ClickAsync();
                await page.GetByRole(AriaRole.Group).Filter(new() { HasText = "MultiSelection AutoFocus (" }).GetByLabel("MultiSelection").ClickAsync();
                await page.Locator(".mud-switch-track-m3.mud-switch-track-primary-m3").First.ClickAsync();
                await page.GetByRole(AriaRole.Group).Filter(new() { HasText = "MultiSelection AutoFocus (" }).GetByLabel("MultiSelection").CheckAsync();
                await page.Locator("#selectextguo6n8p9 > .mud-input-control > .mud-input-control-input-container > .mud-input.mud-input-outlined > .d-flex > div").First.ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Some Placeholder" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Some Placeholder" }).FillAsync("Ala");
                await page.Locator("#checkbox8ikjz73q > .mud-button-root > .mud-checkbox-input").CheckAsync();
                await page.Locator("#checkbox8ikjz73q > .mud-button-root > .mud-checkbox-input").CheckAsync();
                await page.Locator("#checkboxsby3sf8q > .mud-button-root > .mud-checkbox-input").CheckAsync();
                await page.Locator("#overlayfwvewz7l").ClickAsync();
                await page.Locator("#overlayfwvewz7l").ClickAsync();
                await page.GetByText("Standard", new() { Exact = true }).ClickAsync();
                await page.Locator("#listitem_f0f512be").GetByText("Karl Malone - Total Score:").ClickAsync();
                await page.GetByText("B, C").ClickAsync();
                await page.Locator("#checkboxrm0uu276 > .mud-button-root > .mud-checkbox-input").CheckAsync();
                await page.Locator("#overlay57ug213v").ClickAsync();
                await page.Locator("div").Filter(new() { HasTextRegex = new Regex("^B, C, A, Null$") }).Nth(2).PressAsync("Enter");
                await page.Locator("#selectext5ihitlwh").GetByText("Alaska, California").ClickAsync();
                await page.GetByRole(AriaRole.Paragraph).Filter(new() { HasText = "Arkansas" }).ClickAsync();
                await page.Locator("#overlayf1wc6raf").ClickAsync();
                await page.Locator("#overlayiol3765b").ClickAsync();

                return Ok("BrowserController is up and running!");
            }
        }
    }

}
