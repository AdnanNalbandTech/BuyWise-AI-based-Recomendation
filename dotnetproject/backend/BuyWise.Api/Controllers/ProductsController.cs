using BuyWise.Api.Data;
using BuyWise.Api.Models;
using BuyWise.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly TokenService _tokenService;

    public ProductsController(IProductRepository productRepository, TokenService tokenService)
    {
        _productRepository = productRepository;
        _tokenService = tokenService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? brand,
        [FromQuery] double? minRating,
        [FromQuery] string? tags)
    {
        var products = await _productRepository.GetProductsAsync(new ProductSearchRequest
        {
            Search = search,
            CategoryId = categoryId,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Brand = brand,
            MinRating = minRating,
            Tags = tags
        });
        return Ok(products);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<Product>>> SearchProducts([FromQuery] ProductSearchRequest request)
    {
        var products = await _productRepository.GetProductsAsync(request);
        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _productRepository.GetProductAsync(id);
        return product is null ? NotFound(new { message = "Product not found." }) : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(ProductUpsertRequest request)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access is required." });
        }

        var validation = ValidateProduct(request);
        if (validation is not null)
        {
            return BadRequest(new { message = validation });
        }

        var product = await _productRepository.CreateProductAsync(request);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> UpdateProduct(int id, ProductUpsertRequest request)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access is required." });
        }

        var validation = ValidateProduct(request);
        if (validation is not null)
        {
            return BadRequest(new { message = validation });
        }

        var product = await _productRepository.UpdateProductAsync(id, request);
        return product is null ? NotFound(new { message = "Product not found." }) : Ok(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        if (!IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Admin access is required." });
        }

        var deleted = await _productRepository.DeleteProductAsync(id);
        return deleted ? NoContent() : NotFound(new { message = "Product not found." });
    }

    private bool IsAdmin()
    {
        var token = TokenService.ReadBearerToken(Request);
        var principal = _tokenService.ValidateToken(token);
        return principal?.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? ValidateProduct(ProductUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Product name is required.";
        }

        if (request.Price <= 0)
        {
            return "Price must be greater than zero.";
        }

        if (request.Stock < 0)
        {
            return "Stock cannot be negative.";
        }

        if (request.CategoryId <= 0)
        {
            return "A category is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            return "Image URL is required.";
        }

        return null;
    }
}
