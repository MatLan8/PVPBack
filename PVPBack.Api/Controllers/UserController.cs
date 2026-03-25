using Microsoft.AspNetCore.Mvc;
using PVPBack.Core.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PVPBack.Core.Interfaces;
using PVPBack.Domain.Entities;
using PVPBack.Domain.Dtos;

namespace PVPBack.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly SessionService _sessionService;

    /*public UserController(SessionService sessionService)
    {
        _sessionService = sessionService;
    }*/
    
    private readonly IAppDbContext _dbContext;

    public UserController(SessionService sessionService, IAppDbContext dbContext)
    {
        _sessionService = sessionService;
        _dbContext = dbContext;
    }

    [HttpGet("{leaderId:guid}/credits")]
    public async Task<ActionResult<GetCreditsResponse>> GetCredits(
        [FromRoute] Guid leaderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var credits = await _sessionService.GetRemainingCreditsAsync(leaderId, cancellationToken);

            return Ok(new GetCreditsResponse
            {
                LeaderId = leaderId,
                RemainingCredits = credits
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = ex.Message
            });
        }
    }

    public class GetCreditsResponse
    {
        public Guid LeaderId { get; set; }
        public int RemainingCredits { get; set; }
    }

    public class ErrorResponse
    {
        public string Error { get; set; } = null!;
    }
    
    [HttpPost("register")]
    public async Task<ActionResult> Register(
        [FromBody] RegisterDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Check if email already exists
        var exists = await _dbContext.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (exists)
        {
            return BadRequest(new { error = "Email already in use." });
        }

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            Password = hashedPassword,
            RemainingCredits = 10 //PAKEISTI!!!!
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            user.Id
        });
    }
    
    [HttpPost("login")]
    public async Task<ActionResult> Login(
        [FromBody] LoginDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
        {
            return BadRequest(new { error = "Invalid email or password." });
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);

        if (!isPasswordValid)
        {
            return BadRequest(new { error = "Invalid email or password." });
        }

        return Ok(new
        {
            id = user.Id
        });
    }
    
    [HttpGet("{userId:guid}/getUser")]
    public async Task<ActionResult<UserDto>> GetUser(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            
            if (user is null)
                throw new InvalidOperationException("User not found.");

            else
            {
                return Ok(new UserDto()
                {
                    DisplayName = user.DisplayName,
                    RemainingCredits = user.RemainingCredits
                });
            }
            
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ErrorResponse
            {
                Error = ex.Message
            });
        }
    }
}