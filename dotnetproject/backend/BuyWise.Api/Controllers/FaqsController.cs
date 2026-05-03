using BuyWise.Api.Data;
using BuyWise.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class FaqsController : ControllerBase
{
    private readonly IFaqRepository _faqRepository;

    public FaqsController(IFaqRepository faqRepository)
    {
        _faqRepository = faqRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FaqDto>>> GetFaqs()
    {
        var faqs = await _faqRepository.GetAllAsync();
        return Ok(faqs);
    }
}
