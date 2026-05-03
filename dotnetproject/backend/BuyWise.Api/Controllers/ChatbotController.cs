using BuyWise.Api.Models;
using BuyWise.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatbotController : ControllerBase
{
    private readonly ChatbotService _chatbotService;
    private readonly TokenService _tokenService;

    public ChatbotController(ChatbotService chatbotService, TokenService tokenService)
    {
        _chatbotService = chatbotService;
        _tokenService = tokenService;
    }

    [HttpPost("query")]
    public async Task<ActionResult<ChatbotResponse>> Query(ChatbotRequest request)
    {
        var principal = _tokenService.ValidateToken(TokenService.ReadBearerToken(Request));
        var userId = principal?.UserId ?? request.UserId;
        var response = await _chatbotService.ProcessAsync(request with { UserId = userId });
        return Ok(response);
    }
}
