using BuyWise.Api.Data;
using BuyWise.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly IProductRepository _productRepository;

    public CategoriesController(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Category>>> GetCategories()
    {
        var categories = await _productRepository.GetCategoriesAsync();
        return Ok(categories);
    }
}
