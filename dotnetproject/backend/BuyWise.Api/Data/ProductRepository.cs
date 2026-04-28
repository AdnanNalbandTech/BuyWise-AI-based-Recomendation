using BuyWise.Api.Models;
using MySqlConnector;

namespace BuyWise.Api.Data;

public interface IProductRepository
{
    Task<IReadOnlyList<Category>> GetCategoriesAsync();
    Task<IReadOnlyList<Product>> GetProductsAsync(string? search = null, int? categoryId = null);
    Task<Product?> GetProductAsync(int id);
    Task<Product> CreateProductAsync(ProductUpsertRequest request);
    Task<Product?> UpdateProductAsync(int id, ProductUpsertRequest request);
    Task<bool> DeleteProductAsync(int id);
}

public sealed class ProductRepository : IProductRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public ProductRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync()
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, slug, description, image_url
            FROM categories
            ORDER BY name;
            """;

        var categories = new List<Category>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(new Category(
                reader.GetInt32("id"),
                reader.GetString("name"),
                reader.GetString("slug"),
                reader.GetString("description"),
                reader.GetString("image_url")));
        }

        return categories;
    }

    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? search = null, int? categoryId = null)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.name, p.description, p.price, p.image_url, p.stock, p.category_id,
                   c.name AS category_name, p.rating, p.review_count, p.brand, p.tags, p.featured, p.created_at
            FROM products p
            INNER JOIN categories c ON c.id = p.category_id
            WHERE (@CategoryId IS NULL OR p.category_id = @CategoryId)
              AND (
                    @Search IS NULL
                    OR p.name LIKE @Search
                    OR p.description LIKE @Search
                    OR p.brand LIKE @Search
                    OR p.tags LIKE @Search
                    OR c.name LIKE @Search
                  )
            ORDER BY p.featured DESC, p.rating DESC, p.created_at DESC;
            """;
        command.Parameters.AddWithValue("@CategoryId", categoryId.HasValue ? categoryId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : $"%{search.Trim()}%");

        var products = new List<Product>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(MapProduct(reader));
        }

        return products;
    }

    public async Task<Product?> GetProductAsync(int id)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.name, p.description, p.price, p.image_url, p.stock, p.category_id,
                   c.name AS category_name, p.rating, p.review_count, p.brand, p.tags, p.featured, p.created_at
            FROM products p
            INNER JOIN categories c ON c.id = p.category_id
            WHERE p.id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapProduct(reader) : null;
    }

    public async Task<Product> CreateProductAsync(ProductUpsertRequest request)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO products
                (name, description, price, image_url, stock, category_id, rating, review_count, brand, tags, featured)
            VALUES
                (@Name, @Description, @Price, @ImageUrl, @Stock, @CategoryId, @Rating, @ReviewCount, @Brand, @Tags, @Featured);
            SELECT LAST_INSERT_ID();
            """;
        BindProduct(command, request);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return await GetProductAsync(id)
            ?? throw new InvalidOperationException("Product was created but could not be loaded.");
    }

    public async Task<Product?> UpdateProductAsync(int id, ProductUpsertRequest request)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE products
            SET name = @Name,
                description = @Description,
                price = @Price,
                image_url = @ImageUrl,
                stock = @Stock,
                category_id = @CategoryId,
                rating = @Rating,
                review_count = @ReviewCount,
                brand = @Brand,
                tags = @Tags,
                featured = @Featured
            WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);
        BindProduct(command, request);

        var rows = await command.ExecuteNonQueryAsync();
        return rows == 0 ? null : await GetProductAsync(id);
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM products WHERE id = @Id;";
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void BindProduct(MySqlCommand command, ProductUpsertRequest request)
    {
        command.Parameters.AddWithValue("@Name", request.Name.Trim());
        command.Parameters.AddWithValue("@Description", request.Description.Trim());
        command.Parameters.AddWithValue("@Price", request.Price);
        command.Parameters.AddWithValue("@ImageUrl", request.ImageUrl.Trim());
        command.Parameters.AddWithValue("@Stock", request.Stock);
        command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
        command.Parameters.AddWithValue("@Rating", request.Rating);
        command.Parameters.AddWithValue("@ReviewCount", request.ReviewCount);
        command.Parameters.AddWithValue("@Brand", request.Brand.Trim());
        command.Parameters.AddWithValue("@Tags", request.Tags.Trim());
        command.Parameters.AddWithValue("@Featured", request.Featured);
    }

    private static Product MapProduct(MySqlDataReader reader) => new(
        reader.GetInt32("id"),
        reader.GetString("name"),
        reader.GetString("description"),
        reader.GetDecimal("price"),
        reader.GetString("image_url"),
        reader.GetInt32("stock"),
        reader.GetInt32("category_id"),
        reader.GetString("category_name"),
        reader.GetDouble("rating"),
        reader.GetInt32("review_count"),
        reader.GetString("brand"),
        reader.GetString("tags"),
        reader.GetBoolean("featured"),
        reader.GetDateTime("created_at"));
}
