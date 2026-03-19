using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ReportConversion.Application.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ReportConversion.API.Controllers;

/// <summary>
/// Issues JWT tokens for API authentication.
/// </summary>
[ApiController]
[Route("")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates and returns a JWT bearer token.
    /// </summary>
    /// <remarks>
    /// In production, replace the hardcoded credential check with a proper identity store.
    /// </remarks>
    [HttpPost("api/auth/token")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetToken([FromBody] LoginRequest request)
    {
        // Simplified demo credential check — replace with real identity management
        if (request.Username != "admin" || request.Password != "admin")
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized(ApiResponse<object>.Fail("Invalid credentials"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var mins) ? mins : 480;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: new[]
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(ClaimTypes.Role, "Admin")
            },
            expires: DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Token issued for {Username}", request.Username);

        return Ok(ApiResponse<TokenResponse>.Ok(new TokenResponse
        {
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiry)
        }));
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
