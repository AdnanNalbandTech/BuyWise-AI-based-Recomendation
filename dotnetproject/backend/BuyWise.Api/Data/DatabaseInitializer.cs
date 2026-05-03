using BuyWise.Api.Services;
using MySqlConnector;

namespace BuyWise.Api.Data;

public sealed class DatabaseInitializer
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly PasswordService _passwordService;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IConnectionFactory connectionFactory,
        PasswordService passwordService,
        ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await EnsureDatabaseAsync();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await ExecuteAsync(connection, SchemaSql);
        await EnsureOrderColumnsAsync(connection);
        await SeedCategoriesAsync(connection);
        await SeedProductsAsync(connection);
        await RefreshProductTagsAsync(connection);
        await SeedFrequentlyBoughtTogetherAsync(connection);
        await SeedFaqsAsync(connection);
        await SeedAdminUserAsync(connection);

        _logger.LogInformation("BUYWISE database is ready.");
    }

    private async Task EnsureDatabaseAsync()
    {
        var builder = new MySqlConnectionStringBuilder(_connectionFactory.ConnectionString);
        var databaseName = builder.Database;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The MySQL connection string must include a Database value.");
        }

        builder.Database = string.Empty;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ColumnExistsAsync(MySqlConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @TableName
              AND column_name = @ColumnName;
            """;
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", columnName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task EnsureColumnAsync(MySqlConnection connection, string tableName, string columnName, string definition)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName))
        {
            return;
        }

        await ExecuteAsync(connection, $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {definition};");
    }

    private static async Task EnsureOrderColumnsAsync(MySqlConnection connection)
    {
        await EnsureColumnAsync(connection, "orders", "status", "VARCHAR(40) NOT NULL DEFAULT 'Pending'");
        await EnsureColumnAsync(connection, "orders", "tracking_number", "VARCHAR(80) NULL");
        await EnsureColumnAsync(connection, "orders", "estimated_delivery", "DATE NULL");
        await EnsureColumnAsync(connection, "orders", "canceled_at", "TIMESTAMP NULL");
    }

    private static async Task SeedCategoriesAsync(MySqlConnection connection)
    {
        const string sql = """
            INSERT INTO categories (name, slug, description, image_url)
            SELECT @Name, @Slug, @Description, @ImageUrl
            WHERE NOT EXISTS (SELECT 1 FROM categories WHERE slug = @Slug);
            """;

        foreach (var category in Categories)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@Name", category.Name);
            command.Parameters.AddWithValue("@Slug", category.Slug);
            command.Parameters.AddWithValue("@Description", category.Description);
            command.Parameters.AddWithValue("@ImageUrl", category.ImageUrl);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedProductsAsync(MySqlConnection connection)
    {
        const string sql = """
            INSERT INTO products (name, description, price, image_url, stock, category_id, rating, review_count, brand, tags, featured)
            SELECT @Name, @Description, @Price, @ImageUrl, @Stock, c.id, @Rating, @ReviewCount, @Brand, @Tags, @Featured
            FROM categories c
            WHERE c.slug = @CategorySlug
              AND NOT EXISTS (SELECT 1 FROM products WHERE name = @Name);
            """;

        foreach (var product in Products)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@Name", product.Name);
            command.Parameters.AddWithValue("@Description", product.Description);
            command.Parameters.AddWithValue("@Price", product.Price);
            command.Parameters.AddWithValue("@ImageUrl", product.ImageUrl);
            command.Parameters.AddWithValue("@Stock", product.Stock);
            command.Parameters.AddWithValue("@CategorySlug", product.CategorySlug);
            command.Parameters.AddWithValue("@Rating", product.Rating);
            command.Parameters.AddWithValue("@ReviewCount", product.ReviewCount);
            command.Parameters.AddWithValue("@Brand", product.Brand);
            command.Parameters.AddWithValue("@Tags", product.Tags);
            command.Parameters.AddWithValue("@Featured", product.Featured);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task RefreshProductTagsAsync(MySqlConnection connection)
    {
        await using var productsCommand = connection.CreateCommand();
        productsCommand.CommandText = "SELECT id, tags FROM products;";

        var productTags = new List<(int ProductId, string Tag)>();
        await using (var reader = await productsCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var productId = reader.GetInt32("id");
                var tags = reader.GetString("tags")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(tag => tag.ToLowerInvariant())
                    .Distinct();

                productTags.AddRange(tags.Select(tag => (productId, tag)));
            }
        }

        const string sql = """
            INSERT INTO product_tags (product_id, tag)
            SELECT @ProductId, @Tag
            WHERE NOT EXISTS (
                SELECT 1 FROM product_tags WHERE product_id = @ProductId AND tag = @Tag
            );
            """;

        foreach (var (productId, tag) in productTags)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@ProductId", productId);
            command.Parameters.AddWithValue("@Tag", tag);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedFrequentlyBoughtTogetherAsync(MySqlConnection connection)
    {
        const string sql = """
            INSERT INTO frequently_bought_together (primary_product_id, related_product_id, confidence, reason)
            SELECT p1.id, p2.id, @Confidence, @Reason
            FROM products p1
            INNER JOIN products p2
            WHERE p1.name = @PrimaryName
              AND p2.name = @RelatedName
              AND NOT EXISTS (
                  SELECT 1
                  FROM frequently_bought_together f
                  WHERE f.primary_product_id = p1.id AND f.related_product_id = p2.id
              );
            """;

        foreach (var pair in FrequentlyBoughtTogether)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@PrimaryName", pair.PrimaryName);
            command.Parameters.AddWithValue("@RelatedName", pair.RelatedName);
            command.Parameters.AddWithValue("@Confidence", pair.Confidence);
            command.Parameters.AddWithValue("@Reason", pair.Reason);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedFaqsAsync(MySqlConnection connection)
    {
        const string sql = """
            INSERT INTO faqs (question, answer, keywords)
            SELECT @Question, @Answer, @Keywords
            WHERE NOT EXISTS (SELECT 1 FROM faqs WHERE question = @Question);
            """;

        foreach (var faq in Faqs)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@Question", faq.Question);
            command.Parameters.AddWithValue("@Answer", faq.Answer);
            command.Parameters.AddWithValue("@Keywords", faq.Keywords);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task SeedAdminUserAsync(MySqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (full_name, email, password_hash, role)
            SELECT @FullName, @Email, @PasswordHash, 'Admin'
            WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = @Email);
            """;
        command.Parameters.AddWithValue("@FullName", "BUYWISE Admin");
        command.Parameters.AddWithValue("@Email", "admin@buywise.local");
        command.Parameters.AddWithValue("@PasswordHash", _passwordService.HashPassword("Admin@12345"));
        await command.ExecuteNonQueryAsync();
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS categories (
            id INT AUTO_INCREMENT PRIMARY KEY,
            name VARCHAR(120) NOT NULL,
            slug VARCHAR(140) NOT NULL UNIQUE,
            description VARCHAR(500) NOT NULL,
            image_url VARCHAR(800) NOT NULL
        );

        CREATE TABLE IF NOT EXISTS products (
            id INT AUTO_INCREMENT PRIMARY KEY,
            name VARCHAR(180) NOT NULL,
            description VARCHAR(1200) NOT NULL,
            price DECIMAL(10, 2) NOT NULL,
            image_url VARCHAR(800) NOT NULL,
            stock INT NOT NULL DEFAULT 0,
            category_id INT NOT NULL,
            rating DOUBLE NOT NULL DEFAULT 0,
            review_count INT NOT NULL DEFAULT 0,
            brand VARCHAR(120) NOT NULL,
            tags VARCHAR(800) NOT NULL,
            featured BOOLEAN NOT NULL DEFAULT FALSE,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_products_categories FOREIGN KEY (category_id) REFERENCES categories(id)
        );

        CREATE TABLE IF NOT EXISTS product_tags (
            id INT AUTO_INCREMENT PRIMARY KEY,
            product_id INT NOT NULL,
            tag VARCHAR(120) NOT NULL,
            CONSTRAINT fk_product_tags_products FOREIGN KEY (product_id) REFERENCES products(id),
            UNIQUE KEY ux_product_tags_product_tag (product_id, tag)
        );

        CREATE TABLE IF NOT EXISTS users (
            id INT AUTO_INCREMENT PRIMARY KEY,
            full_name VARCHAR(180) NOT NULL,
            email VARCHAR(180) NOT NULL UNIQUE,
            password_hash VARCHAR(500) NOT NULL,
            role VARCHAR(40) NOT NULL DEFAULT 'Customer',
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS user_activities (
            id INT AUTO_INCREMENT PRIMARY KEY,
            user_id INT NOT NULL,
            product_id INT NOT NULL,
            activity_type VARCHAR(40) NOT NULL,
            quantity INT NOT NULL DEFAULT 1,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_user_activities_users FOREIGN KEY (user_id) REFERENCES users(id),
            CONSTRAINT fk_user_activities_products FOREIGN KEY (product_id) REFERENCES products(id)
        );

        CREATE TABLE IF NOT EXISTS cart_items (
            id INT AUTO_INCREMENT PRIMARY KEY,
            user_id INT NOT NULL,
            product_id INT NOT NULL,
            quantity INT NOT NULL DEFAULT 1,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            CONSTRAINT fk_cart_items_users FOREIGN KEY (user_id) REFERENCES users(id),
            CONSTRAINT fk_cart_items_products FOREIGN KEY (product_id) REFERENCES products(id),
            UNIQUE KEY ux_cart_items_user_product (user_id, product_id)
        );

        CREATE TABLE IF NOT EXISTS orders (
            id INT AUTO_INCREMENT PRIMARY KEY,
            user_id INT NULL,
            full_name VARCHAR(180) NOT NULL,
            email VARCHAR(180) NOT NULL,
            shipping_address VARCHAR(700) NOT NULL,
            total DECIMAL(10, 2) NOT NULL,
            status VARCHAR(40) NOT NULL DEFAULT 'Pending',
            tracking_number VARCHAR(80) NULL,
            estimated_delivery DATE NULL,
            canceled_at TIMESTAMP NULL,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT fk_orders_users FOREIGN KEY (user_id) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS order_items (
            id INT AUTO_INCREMENT PRIMARY KEY,
            order_id INT NOT NULL,
            product_id INT NOT NULL,
            product_name VARCHAR(180) NOT NULL,
            quantity INT NOT NULL,
            unit_price DECIMAL(10, 2) NOT NULL,
            CONSTRAINT fk_order_items_orders FOREIGN KEY (order_id) REFERENCES orders(id),
            CONSTRAINT fk_order_items_products FOREIGN KEY (product_id) REFERENCES products(id)
        );

        CREATE TABLE IF NOT EXISTS frequently_bought_together (
            id INT AUTO_INCREMENT PRIMARY KEY,
            primary_product_id INT NOT NULL,
            related_product_id INT NOT NULL,
            confidence DOUBLE NOT NULL DEFAULT 0,
            reason VARCHAR(300) NOT NULL,
            CONSTRAINT fk_fbt_primary_products FOREIGN KEY (primary_product_id) REFERENCES products(id),
            CONSTRAINT fk_fbt_related_products FOREIGN KEY (related_product_id) REFERENCES products(id),
            UNIQUE KEY ux_fbt_pair (primary_product_id, related_product_id)
        );

        CREATE TABLE IF NOT EXISTS faqs (
            id INT AUTO_INCREMENT PRIMARY KEY,
            question VARCHAR(220) NOT NULL,
            answer VARCHAR(1200) NOT NULL,
            keywords VARCHAR(500) NOT NULL
        );
        """;

    private static readonly CategorySeed[] Categories =
    [
        new("Electronics", "electronics", "Smart gadgets, cameras, tablets, and creator tech.", "https://images.unsplash.com/photo-1550009158-9ebf69173e03?auto=format&fit=crop&w=900&q=80"),
        new("Mobiles", "mobiles", "5G phones, cases, chargers, and mobile accessories.", "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=900&q=80"),
        new("Laptops", "laptops", "Work, gaming, creator, and study-ready portable computers.", "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=900&q=80"),
        new("Shoes", "shoes", "Running, training, casual, and lifestyle footwear.", "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=900&q=80"),
        new("Clothes", "clothes", "Everyday apparel, outerwear, activewear, and essentials.", "https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=900&q=80"),
        new("Watches", "watches", "Smartwatches, analog watches, and premium timepieces.", "https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=900&q=80"),
        new("Home Appliances", "home-appliances", "Kitchen, cleaning, comfort, and smart home appliances.", "https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?auto=format&fit=crop&w=900&q=80"),
        new("Accessories", "accessories", "Bags, chargers, keyboards, headphones, cases, and add-ons.", "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=900&q=80"),
        new("Smartphones", "smartphones", "Legacy smartphone category kept for existing catalog compatibility.", "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=900&q=80"),
        new("Audio", "audio", "Headphones, speakers, earbuds, and studio sound gear.", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=900&q=80"),
        new("Fashion", "fashion", "Legacy fashion category kept for existing catalog compatibility.", "https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=900&q=80"),
        new("Home", "home", "Legacy home category kept for existing catalog compatibility.", "https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?auto=format&fit=crop&w=900&q=80"),
        new("Fitness", "fitness", "Wearables, workout accessories, and recovery products.", "https://images.unsplash.com/photo-1517838277536-f5f99be501cd?auto=format&fit=crop&w=900&q=80")
    ];

    private static readonly ProductSeed[] Products =
    [
        new("Samsung Galaxy S25 5G", "Flagship Android phone with AI photo editing, vivid AMOLED display, and fast all-day performance.", 74999, "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?auto=format&fit=crop&w=900&q=80", 32, "mobiles", 4.8, 621, "Samsung", "mobile,phone,5g,android,camera,ai,premium", true),
        new("iPhone 16", "A premium iOS smartphone with a bright display, advanced camera controls, and long software support.", 79900, "https://images.unsplash.com/photo-1603891128711-11b4b03bb138?auto=format&fit=crop&w=900&q=80", 18, "mobiles", 4.7, 488, "Apple", "mobile,phone,ios,camera,premium,ai", true),
        new("OnePlus Nord CE 5G", "Slim 5G phone with fast charging, clean software, and dependable everyday cameras.", 24999, "https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=900&q=80", 55, "mobiles", 4.5, 352, "OnePlus", "mobile,phone,5g,fast-charging,android,value", true),
        new("Redmi Note Pro Max", "High-value smartphone with a large battery, sharp display, and versatile camera system.", 18999, "https://images.unsplash.com/photo-1616348436168-de43ad0db179?auto=format&fit=crop&w=900&q=80", 72, "mobiles", 4.4, 410, "Redmi", "mobile,phone,android,budget,battery,camera", false),
        new("AeroCharge 30W USB-C Charger", "Compact fast charger for phones, tablets, earbuds, and travel-ready everyday charging.", 1499, "https://images.unsplash.com/photo-1615529328331-f8917597711f?auto=format&fit=crop&w=900&q=80", 130, "accessories", 4.4, 201, "Aero", "charger,usb-c,phone,accessory,fast", false),
        new("ShieldCase Clear Armor", "Transparent mobile case with raised camera edges and reinforced drop-protection corners.", 799, "https://images.unsplash.com/photo-1601593346740-925612772716?auto=format&fit=crop&w=900&q=80", 160, "accessories", 4.3, 144, "ShieldCase", "case,phone,accessory,clear,protection", false),
        new("MacBook Air M3 13", "Ultralight laptop for students, creators, and professionals who need silent battery-efficient performance.", 114900, "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=900&q=80", 15, "laptops", 4.9, 374, "Apple", "laptop,work,student,creator,lightweight,ssd", true),
        new("Dell Inspiron 15", "Reliable everyday laptop with a full keyboard, fast SSD storage, and office-ready performance.", 52999, "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=900&q=80", 26, "laptops", 4.5, 288, "Dell", "laptop,work,student,office,ssd,value", true),
        new("ASUS ROG Strix G16", "Gaming laptop with RTX graphics, high-refresh display, RGB keyboard, and advanced thermal cooling.", 149990, "https://images.unsplash.com/photo-1603302576837-37561b2e2302?auto=format&fit=crop&w=900&q=80", 9, "laptops", 4.8, 211, "ASUS", "laptop,gaming,rtx,rgb,performance,high-refresh", true),
        new("HP Victus Gaming 15", "Performance laptop for gaming, editing, and multitasking with dedicated graphics and strong cooling.", 74999, "https://images.unsplash.com/photo-1593642632823-8f785ba67e45?auto=format&fit=crop&w=900&q=80", 19, "laptops", 4.6, 189, "HP", "laptop,gaming,creator,performance,rtx,value", false),
        new("Logitech MX Master 3S", "Ergonomic wireless mouse with quiet clicks, precision scrolling, and multi-device workflow support.", 8995, "https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?auto=format&fit=crop&w=900&q=80", 46, "accessories", 4.8, 333, "Logitech", "mouse,laptop,wireless,ergonomic,productivity", true),
        new("DailyFit Laptop Backpack", "Organized commuter backpack with laptop sleeve, bottle pocket, and travel-safe compartments.", 2499, "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=900&q=80", 80, "accessories", 4.5, 214, "DailyFit", "bag,laptop,travel,commute,accessory", false),
        new("Sony WH-1000XM5 Headphones", "Premium noise-cancelling headphones with rich sound, multipoint Bluetooth, and long battery life.", 29990, "https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=900&q=80", 23, "electronics", 4.9, 620, "Sony", "headphones,audio,anc,wireless,music,premium", true),
        new("boAt Stone Bluetooth Speaker", "Portable speaker with punchy bass, water resistance, and travel-friendly battery life.", 2499, "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=900&q=80", 68, "electronics", 4.4, 492, "boAt", "speaker,audio,bluetooth,portable,bass", false),
        new("Canon EOS R50 Creator Kit", "Mirrorless camera kit for vloggers, product shots, streaming, and sharp everyday photography.", 66995, "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=900&q=80", 12, "electronics", 4.7, 97, "Canon", "camera,creator,vlogging,mirrorless,video", true),
        new("Samsung Galaxy Tab S9 FE", "Large-screen tablet for notes, media, study, and sketching with stylus-ready productivity.", 36999, "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?auto=format&fit=crop&w=900&q=80", 24, "electronics", 4.6, 173, "Samsung", "tablet,android,study,stylus,media", false),
        new("Nike Pegasus Running Shoes", "Daily running shoes with responsive cushioning, breathable upper, and durable outsole grip.", 10495, "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=900&q=80", 44, "shoes", 4.7, 341, "Nike", "shoes,running,fitness,cushion,breathable", true),
        new("Adidas Ultraboost Light", "Premium running and lifestyle shoes with soft energy-return cushioning and sock-like fit.", 17999, "https://images.unsplash.com/photo-1608231387042-66d1773070a5?auto=format&fit=crop&w=900&q=80", 31, "shoes", 4.8, 286, "Adidas", "shoes,running,lifestyle,cushion,premium", true),
        new("Puma Flex Trainer", "Stable training shoes for gym sessions, walking, and everyday casual comfort.", 4499, "https://images.unsplash.com/photo-1562183241-b937e95585b6?auto=format&fit=crop&w=900&q=80", 66, "shoes", 4.4, 198, "Puma", "shoes,training,gym,walking,comfort", false),
        new("Woodland Trek Boots", "Rugged outdoor boots with ankle support, grippy sole, and weather-ready durability.", 6995, "https://images.unsplash.com/photo-1520639888713-7851133b1ed0?auto=format&fit=crop&w=900&q=80", 27, "shoes", 4.5, 132, "Woodland", "shoes,boots,trekking,outdoor,durable", false),
        new("Levi's 511 Slim Jeans", "Slim-fit denim with comfortable stretch, durable stitching, and a clean everyday look.", 3299, "https://images.unsplash.com/photo-1542272604-787c3835535d?auto=format&fit=crop&w=900&q=80", 60, "clothes", 4.5, 209, "Levi's", "clothes,jeans,denim,casual,slim", true),
        new("Uniqlo Airism T-Shirt", "Lightweight breathable T-shirt for everyday wear, layering, travel, and warm weather.", 1490, "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=900&q=80", 120, "clothes", 4.6, 311, "Uniqlo", "clothes,tshirt,breathable,casual,summer", false),
        new("H&M Cotton Hoodie", "Soft cotton-blend hoodie with a relaxed fit, kangaroo pocket, and clean streetwear styling.", 2299, "https://images.unsplash.com/photo-1556821840-3a63f95609a7?auto=format&fit=crop&w=900&q=80", 75, "clothes", 4.4, 187, "H&M", "clothes,hoodie,winter,streetwear,casual", true),
        new("Nike Dri-FIT Training Tee", "Moisture-wicking training top for running, gym workouts, and active everyday movement.", 1895, "https://images.unsplash.com/photo-1515886657613-9f3515b0c78f?auto=format&fit=crop&w=900&q=80", 83, "clothes", 4.5, 155, "Nike", "clothes,training,fitness,dryfit,running", false),
        new("Apple Watch Series 10", "Advanced smartwatch with health tracking, notifications, workout metrics, and smooth iPhone pairing.", 46900, "https://images.unsplash.com/photo-1434493789847-2f02dc6ca35d?auto=format&fit=crop&w=900&q=80", 18, "watches", 4.8, 254, "Apple", "watch,smartwatch,fitness,health,ios,premium", true),
        new("Samsung Galaxy Watch 7", "Wear OS smartwatch with sleep tracking, body insights, GPS workouts, and app support.", 29999, "https://images.unsplash.com/photo-1508685096489-7aacd43bd3b1?auto=format&fit=crop&w=900&q=80", 28, "watches", 4.6, 221, "Samsung", "watch,smartwatch,android,fitness,health,gps", true),
        new("Fossil Grant Chronograph", "Classic analog chronograph watch with leather strap and refined formal styling.", 11995, "https://images.unsplash.com/photo-1523170335258-f5ed11844a49?auto=format&fit=crop&w=900&q=80", 34, "watches", 4.5, 148, "Fossil", "watch,analog,chronograph,leather,formal", false),
        new("Casio G-Shock GA-2100", "Tough everyday watch with shock resistance, slim octagonal case, and sporty styling.", 8995, "https://images.unsplash.com/photo-1533139502658-0198f920d8e8?auto=format&fit=crop&w=900&q=80", 42, "watches", 4.7, 308, "Casio", "watch,digital,sport,tough,water-resistant", false),
        new("LG 260L Frost-Free Refrigerator", "Energy-efficient refrigerator with smart inverter cooling, spacious shelves, and humidity control.", 29990, "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=900&q=80", 14, "home-appliances", 4.5, 126, "LG", "home,appliance,refrigerator,kitchen,inverter", true),
        new("Dyson V12 Detect Slim", "Cordless vacuum with laser dust detection, strong suction, and lightweight deep-cleaning design.", 58900, "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=900&q=80", 10, "home-appliances", 4.8, 176, "Dyson", "home,appliance,vacuum,cordless,cleaning,premium", true),
        new("Philips Air Fryer XL", "Family-size air fryer for low-oil snacks, crisp fries, reheating, baking, and quick dinners.", 12995, "https://images.unsplash.com/photo-1585515320310-259814833e62?auto=format&fit=crop&w=900&q=80", 25, "home-appliances", 4.6, 203, "Philips", "home,appliance,air-fryer,kitchen,healthy", false),
        new("Bajaj Majesty Mixer Grinder", "Powerful mixer grinder for chutneys, smoothies, masalas, and everyday Indian cooking.", 3499, "https://images.unsplash.com/photo-1590794056226-79ef3a8147e1?auto=format&fit=crop&w=900&q=80", 48, "home-appliances", 4.4, 267, "Bajaj", "home,appliance,mixer,kitchen,cooking", false),
        new("Anker PowerCore 20000", "High-capacity power bank with fast USB-C charging for phones, tablets, and travel days.", 3999, "https://images.unsplash.com/photo-1609091839311-d5365f9ff1c5?auto=format&fit=crop&w=900&q=80", 58, "accessories", 4.6, 315, "Anker", "powerbank,charger,travel,mobile,accessory", true),
        new("Keychron K2 Wireless Keyboard", "Compact wireless mechanical keyboard for workstations, coding, tablets, and laptop setups.", 7999, "https://images.unsplash.com/photo-1618384887929-16ec33fab9ef?auto=format&fit=crop&w=900&q=80", 29, "accessories", 4.7, 229, "Keychron", "keyboard,laptop,wireless,mechanical,desk", false),
        new("JBL Tune Beam Earbuds", "Wireless earbuds with noise cancellation, punchy bass, and a compact pocket charging case.", 6499, "https://images.unsplash.com/photo-1606220588913-b3aacb4d2f46?auto=format&fit=crop&w=900&q=80", 62, "accessories", 4.5, 377, "JBL", "earbuds,audio,anc,wireless,mobile", true),
        new("Spigen Tempered Glass Protector", "Scratch-resistant screen protector with precise touch response and easy bubble-free installation.", 999, "https://images.unsplash.com/photo-1580910051074-3eb694886505?auto=format&fit=crop&w=900&q=80", 140, "accessories", 4.4, 198, "Spigen", "screen-protector,phone,glass,accessory,protection", false)
    ];

    private static readonly BoughtTogetherSeed[] FrequentlyBoughtTogether =
    [
        new("Samsung Galaxy S25 5G", "ShieldCase Clear Armor", 0.91, "Most phone buyers add protection on the same order."),
        new("Samsung Galaxy S25 5G", "AeroCharge 30W USB-C Charger", 0.87, "Fast charger is a common phone add-on."),
        new("iPhone 16", "Spigen Tempered Glass Protector", 0.89, "Screen protection is commonly purchased with premium phones."),
        new("iPhone 16", "Anker PowerCore 20000", 0.74, "Travel power is a strong iPhone companion."),
        new("ASUS ROG Strix G16", "Logitech MX Master 3S", 0.78, "Gaming and creator laptop buyers often add a premium mouse."),
        new("Dell Inspiron 15", "DailyFit Laptop Backpack", 0.84, "Laptop shoppers frequently add a protective backpack."),
        new("MacBook Air M3 13", "Keychron K2 Wireless Keyboard", 0.68, "MacBook users often build a desk setup."),
        new("Nike Pegasus Running Shoes", "Nike Dri-FIT Training Tee", 0.72, "Running shoes and activewear are often bought together."),
        new("Apple Watch Series 10", "Nike Pegasus Running Shoes", 0.64, "Fitness-focused shoppers pair smartwatches with running gear."),
        new("Philips Air Fryer XL", "Bajaj Majesty Mixer Grinder", 0.55, "Kitchen appliance buyers often upgrade multiple cooking tools.")
    ];

    private static readonly FaqSeed[] Faqs =
    [
        new("What is the return policy?", "Most BUYWISE products can be returned within 7 days of delivery if unused, with original packaging and invoice. Some hygiene, personal care, and clearance items may be non-returnable.", "return,refund,policy,exchange"),
        new("How long does delivery take?", "Standard delivery usually takes 3 to 6 business days. Metro cities may receive eligible products in 1 to 3 business days depending on seller stock and courier availability.", "delivery,shipping,time,courier"),
        new("Which payment methods are supported?", "BUYWISE supports UPI, debit cards, credit cards, net banking, wallets, and cash on delivery for eligible pin codes.", "payment,upi,card,cod,wallet"),
        new("How does warranty work?", "Warranty is provided by the product brand or seller. Warranty duration is shown on the product page and support is available through brand service centers or BUYWISE support.", "warranty,guarantee,service,repair"),
        new("Can I cancel my order?", "Orders can be cancelled while they are Pending or Processing. Once shipped, you can refuse delivery or request a return after delivery if the item is eligible.", "cancel,cancellation,order,status")
    ];

    private sealed record CategorySeed(string Name, string Slug, string Description, string ImageUrl);

    private sealed record ProductSeed(
        string Name,
        string Description,
        decimal Price,
        string ImageUrl,
        int Stock,
        string CategorySlug,
        double Rating,
        int ReviewCount,
        string Brand,
        string Tags,
        bool Featured);

    private sealed record BoughtTogetherSeed(string PrimaryName, string RelatedName, double Confidence, string Reason);

    private sealed record FaqSeed(string Question, string Answer, string Keywords);
}
