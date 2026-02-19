# Authentication
- Authentication is the process of validating the identity of a registered user who is accessing a service or an application.
- Authorization is the process of validating the authenticated user if the user has permissions to access a certain service or an application.

---

## Cookie-based Auth
- In a typical cookie-based authentication flow, a browser communicates directly with a server.
    1. The client sends authentication credentials (e.g. username and password) to the server, usually via an endpoint such as /authenticate.
    2. The server validates the credentials. If they are valid, it creates a server-side session, typically stored in memory or a session store.
    3. The server returns a sessionId to the client. This identifier is stored in a browser cookie and is automatically included in subsequent requests.
    4. For every follow-up request, the browser sends the cookie containing the sessionId. The server uses this value to locate the corresponding session and authenticate the user.
- This approach is called cookie-based authentication because authentication relies on a session identifier stored in a cookie.
- Limitations:
    1. Scalability: Each authenticated user requires a server-side session. At large scale (e.g. millions of concurrent users), maintaining and synchronizing these sessions can become expensive and impact performance.
    2. Security Risk: If authentication cookies are compromised (e.g. via XSS or insecure transport), the attacker gains access to the sessionId and can impersonate the user until the session expires or is revoked.

---

## Token-based Auth
- In token-based authentication, the client and server interact without creating server-side sessions.
    1. The client sends authentication credentials (such as username and password) to the server.
    2. The server validates the credentials. If they are valid, the server does not create a session. Instead, it generates a token.
    3. The token is a cryptographically signed (and sometimes encrypted) string that contains enough information for the server to identify the user and validate the request.
    4. The token is returned to the client and stored in the browser (for example, in memory or secure storage).
    5. For subsequent requests, the client sends the token in the Authorization header using the Bearer scheme:
    ```cs
        Authorization: Bearer <token>
    ```
    6. The server validates the token on each request and uses the information inside it to authenticate the user.
  
- Token Lifetime
    1. Tokens have a limited expiration time.
    2. Access tokens are typically short-lived (often 5–15 minutes).
    3. If an access token is compromised, its impact is limited by its short lifetime.

- Access Tokens vs Refresh Token
    1. Access Token: 
        - Short-lived (5–15 minutes)
        - Sent with every request
        - Used to access protected APIs
    2. Refresh Token:
        - Long-lived (days or weeks)
        - Stored securely (database)
        - Used only to get a new access token
     
---

### Json Web Token
- The [JWT](https://jwt.io) is an open standard that defines a compact and self-contained way for securely transmitting information between parties as a JSON object.

> JWT is signed, not encrypted, unless JWE (JSON Web Encryption) is explicitly used.

- It has three parts:
    1. Header: Type of token, signing algorithm
    2. Payload: Claims(Registered, Public, Private)
    3. Signature: Signature(Secret)

---

#### Setup JWT & EF
This section describes how to configure ASP.NET Core Identity, Entity Framework Core, and JWT-based authentication using access and refresh tokens.

- **Step 1: Required NuGet Packages**
    - Install the following packages:
        1. Microsoft.EntityFrameworkCore
        2. Microsoft.EntityFrameworkCore.SqlServer
        3. Microsoft.AspNetCore.Identity.EntityFrameworkCore
        4. Microsoft.EntityFrameworkCore.Tools
        5. Microsoft.AspNetCore.Authentication.JwtBearer

- **Step 2: JWT Configuration**
    ``` cs title="appsettings.json"
        "JwtSettings":{
          "SecretKey": "xxxxxxxxxx",
          "Issuer": "https://localhost:5002/",
          "Audience": "user",
          "AccessTokenMinutes" : "10",
          "RefreshTokenDays" : "180"
        }
    ```

    > ⚠️ Store SecretKey securely (User Secrets or environment variables).

- **Step 3: Settings POCO**
    - Strongly-typed configuration model for JWT settings:

    ``` cs
    public sealed class JwtSettings
    {
        public string SecretKey { get; set; } = null!;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int AccessTokenMinutes { get; set; }
        public int RefreshTokenDays { get; set; }
    }
    ```

- **Step 4: App Configuration**
    ``` cs title="Program.cs"
    var builder = WebApplication.CreateBuilder(args);

    var configuration = new ConfigurationBuilder()
        .SetBasePath(Environment.CurrentDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // lowest priority
        .AddUserSecrets<Program>(optional: true, reloadOnChange: true)         // overrides json
        .AddEnvironmentVariables()                                            // highest priority
        .Build();
    
    builder.Services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
    var jwt = configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
    
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlServer(configuration.GetConnectionString("DBConnection")));
    ```

- **Step 5: Configure Identity**
    ``` cs title="Program.cs"
    builder.Services
        .AddIdentity<User, Role>(options =>
        {
            options.User.RequireUniqueEmail = true;
        
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 2;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
        
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();
    ```

- **Step 6: JWT Token Validation**
    ``` cs title="Program.cs"
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));

    var tokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
    
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    builder.Services.AddSingleton(tokenValidationParameters);
    ```

- **Step 7: Authentication Middleware**
    ``` cs title="Program.cs"
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = true; // disable only in local dev
            options.TokenValidationParameters = tokenValidationParameters;
        });

    builder.Services.AddAuthorization();
    ```

- **Step 8: HTTP Pipeline**
    ``` cs title="Program.cs"
    var app = builder.Build();

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    await AppDbInitializer.SeedRoles(app);

    app.MapControllers();
    app.Run();
    ```

- **Step 9: Role Seeding**
    ``` cs
    public static class AppDbInitializer
    {
        public static async Task SeedRoles(IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

            async Task EnsureRole(string roleName)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new Role { Name = roleName });
                }
            }

            await EnsureRole("Admin");
            await EnsureRole("User");
        }
    }
    ```

- **Step 10: Setup DB Context**

    ``` cs title="AppDbContext.cs"
    public class AppDbContext : IdentityDbContext<
        User, Role, long,
        IdentityUserClaim<long>, IdentityUserRole<long>, IdentityUserLogin<long>,
        IdentityRoleClaim<long>, IdentityUserToken<long>>
    {
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.HasDefaultSchema("Identity");

            // Rename Identity tables (remove AspNet*)
            builder.Entity<User>().ToTable("User");
            builder.Entity<Role>().ToTable("Role");
            builder.Entity<IdentityUserRole<long>>().ToTable("UserRole");
            builder.Entity<IdentityUserClaim<long>>().ToTable("UserClaim");
            builder.Entity<IdentityUserLogin<long>>().ToTable("UserLogin");
            builder.Entity<IdentityRoleClaim<long>>().ToTable("RoleClaim");
            builder.Entity<IdentityUserToken<long>>().ToTable("UserToken");

            // (Optional) tweak PK/indices names if you want consistent naming, EF will generate defaults otherwise
            // Example:
            // builder.Entity<User>().HasKey(u => u.Id).HasName("PK_Users");

            // Ensure FK columns are bigint where needed—Identity generics <long> already do that.
        }
    }
    ```

- **Step 11: Setup Identity Models**
    ``` cs
    public class User : IdentityUser<long>
    {
        public bool IsApproved { get; set; }
    }

    public class Role : IdentityRole<long>
    {
        public long? ParentRoleId { get; set; }
        public string? Description { get; set; }
        public bool IsDeleted { get; set; } 

        public Role? ParentRole { get; set; }
        public ICollection<IdentityRoleClaim<long>> RoleClaims { get; set; }
            = new List<IdentityRoleClaim<long>>();
    }

    public class UserProfile
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public long UserId { get; set; }
        public string? CompanyName { get; set; }
        public required string Firstname { get; set; }
        public required string Lastname { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? Street { get; set; }
        public string? Description { get; set; }
        public virtual User User { get; set; } = null!;
    }
    ```


- **Step 12: Refresh Token**
    ``` cs
    public class RefreshToken
    {
        [Key] public long Id { get; set; }
        public string Token { get; set; } = null!;
        public string JwtId { get; set; } = null!;
        public bool IsRevoked { get; set; }

        public DateTime DateAdded { get; set; }
        public DateTime DateExpire { get; set; }

        public long UserId { get; set; }
        public User User { get; set; } = null!;
    }
    ```
    
---

#### AuthService

- **Step 1: Auth Result Model**
    ``` cs
    public sealed record AuthResult(
        string AccessToken,
        DateTimeOffset ExpiresAt
    );
    ```

- **Step 2: AuthService Interface**
    ``` cs
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string email, string password);
        Task<AuthResult> RefreshAsync(string accessToken, string refreshToken);
        Task LogoutAsync(long userId);
    }
    ```

- **Step 3: AuthService Implementation**
    ``` cs
    public sealed class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly JwtSettings _jwt;
        private readonly TokenValidationParameters _tokenValidationParameters;

        public AuthService(
            AppDbContext context,
            UserManager<User> userManager,
            IOptions<JwtSettings> jwt,
            TokenValidationParameters tokenValidationParameters)
        {
            _context = context;
            _userManager = userManager;
            _jwt = jwt.Value;
            _tokenValidationParameters = tokenValidationParameters;
        }

        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email)
                       ?? throw new UnauthorizedAccessException();

            if (!await _userManager.IsEmailConfirmedAsync(user))
                throw new UnauthorizedAccessException();

            if (!await _userManager.CheckPasswordAsync(user, password))
                throw new UnauthorizedAccessException();

            return await IssueTokensAsync(user);
        }

        public async Task<AuthResult> RefreshAsync(string accessToken, string refreshToken)
        {
            var stored = await _context.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == refreshToken);

            if (stored is null || stored.IsRevoked || stored.DateExpire <= DateTime.UtcNow)
                throw new UnauthorizedAccessException();

            var handler = new JwtSecurityTokenHandler();
            SecurityToken validated;

            var parameters = _tokenValidationParameters.Clone();
            parameters.ValidateLifetime = false;

            try
            {
                handler.ValidateToken(accessToken, parameters, out validated);
            }
            catch
            {
                throw new UnauthorizedAccessException();
            }

            var jwt = (JwtSecurityToken)validated;
            var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            if (stored.JwtId != jti)
                throw new UnauthorizedAccessException();

            stored.IsRevoked = true;
            await _context.SaveChangesAsync();

            return await IssueTokensAsync(stored.User);
        }

        public async Task LogoutAsync(long userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(x => x.UserId == userId && !x.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
                token.IsRevoked = true;

            await _context.SaveChangesAsync();
        }

        private async Task<AuthResult> IssueTokensAsync(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            var refresh = new RefreshToken
            {
                UserId = user.Id,
                JwtId = token.Id,
                Token = $"{Guid.NewGuid()}-{Guid.NewGuid()}",
                DateAdded = DateTime.UtcNow,
                DateExpire = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
            };

            _context.RefreshTokens.Add(refresh);
            await _context.SaveChangesAsync();

            return new AuthResult(
                new JwtSecurityTokenHandler().WriteToken(token),
                token.ValidTo
            );
        }
    }
    ```

---

#### RegistrationService
- **Step 1: RegistrationService Interface**
    ``` cs
    public interface IRegistrationService
    {
        Task RegisterAsync(RegisterModel model);
    }
    ```

- **Step 2: RegistrationService Implementation**
    ```
    public sealed class RegistrationService : IRegistrationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEmailService _emailService;

        public RegistrationService(
            AppDbContext context,
            UserManager<User> userManager,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        public async Task RegisterAsync(RegisterModel model)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                if (await _userManager.FindByEmailAsync(model.Email) != null)
                    throw new InvalidOperationException("User already exists");
            
                var user = new User { Email = model.Email, UserName = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
            
                if (!result.Succeeded)
                    throw new InvalidOperationException("User creation failed");
            
                await _userManager.AddToRoleAsync(user, "User");
            
                _context.UserProfiles.Add(new UserProfile
                {
                    UserId = user.Id,
                    Firstname = model.Firstname,
                    Lastname = model.Lastname
                });
            
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            
                await _emailService.SendConfirmationAsync(user);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
    ```

---

#### EmailService
- **Step 1: EmailService Interface**
    ``` cs
    public interface IEmailService
    {
        Task SendConfirmationAsync(User user);
    }
    ```

- **Step 2: EmailService Implementation**
    ```
    public sealed class EmailService : IEmailService
    {
        private readonly UserManager<User> _userManager;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public EmailService(
            UserManager<User> userManager,
            IConfiguration config,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _config = config;
            _emailSender = emailSender;
        }

        public async Task SendConfirmationAsync(User user)
        {
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var callbackUrl =
                $"{_config["Production:ProductionUrl"]}/account/confirm-email" +
                $"?userId={user.Id}&code={Uri.EscapeDataString(code)}";

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Activate your account",
                BuildHtml("EmailConfirmationTemplate", callbackUrl));
        }

        private static string BuildHtml(string template, string url)
        {
            var html = File.ReadAllText($"Templates/{template}.html");
            return html.Replace("{CallbackUrl}", url);
        }
    }
    ```
- **Step 3: SMTPEmail Sender**
    ``` cs
    public interface IEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage);
    }
    ```
- **Step 4: SMTPEmailSender Implementation**
    ``` cs
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpSettings _settings;
    
        public SmtpEmailSender(IOptions<SmtpSettings> settings)
        {
            _settings = settings.Value;
        }
    
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                Credentials = new NetworkCredential(
                    _settings.Username,
                    _settings.Password),
                _settings.EnableSsl
            };
    
            var message = new MailMessage
            {
                From = new MailAddress(_settings.From),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
    
            message.To.Add(email);
    
            await client.SendMailAsync(message);
        }
    }
    ```
---

#### Controller
``` cs
[ApiController]
[Route("account")]
public sealed class AccountController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IRegistrationService _registration;
    private readonly UserManager<User> _userManager;

    public AccountController(
        IAuthService auth,
        IRegistrationService registration,
        UserManager<User> userManager)
    {
        _auth = auth;
        _registration = registration;
        _userManager = userManager;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        await _registration.RegisterAsync(model);
        return Ok("User created. Please confirm your email.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var result = await _auth.LoginAsync(model.Email, model.Password);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] TokenRequestModel model)
    {
        var result = await _auth.RefreshAsync(model.Token, model.RefreshToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auth.LogoutAsync(userId);
        return Ok();
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] string userId,
        [FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            return BadRequest("Invalid request");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return BadRequest("Invalid user");

        var result = await _userManager.ConfirmEmailAsync(user, code);
        if (!result.Succeeded)
            return BadRequest("Confirmation failed");

        return Ok("Email confirmed successfully");
    }
}
```

---

#### Dependency Injection
``` cs
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
```

---

#### Global exception handling
``` cs
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        context.Response.ContentType = "application/json";

        var response = new
        {
            message = "An error occurred"
        };

        switch (exception)
        {
            case UnauthorizedAccessException:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                response = new { message = "Unauthorized" };
                break;

            case InvalidOperationException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new { message = exception.Message };
                break;

            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        await context.Response.WriteAsJsonAsync(response);
    });
});
```
