using BuyWise.Api.Data;
using BuyWise.Api.Models;
using BuyWise.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordService _passwordService;
    private readonly TokenService _tokenService;

    public AuthController(
        IUserRepository userRepository,
        PasswordService passwordService,
        TokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Full name, email, and password are required." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var user = await _userRepository.CreateAsync(request, _passwordService.HashPassword(request.Password));
        return Ok(ToAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null || !_passwordService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return Ok(ToAuthResponse(user));
    }

    private AuthResponse ToAuthResponse(User user) =>
        new(
            _tokenService.CreateToken(user),
            new PublicUser(user.Id, user.FullName, user.Email, user.Role));
}
