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
        await SeedCategoriesAsync(connection);
        await SeedProductsAsync(connection);
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

    private static async Task<int> CountAsync(MySqlConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM `{tableName}`;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SeedCategoriesAsync(MySqlConnection connection)
    {
        if (await CountAsync(connection, "categories") > 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO categories (name, slug, description, image_url) VALUES
            ('Smartphones', 'smartphones', 'Latest phones, foldables, chargers, and mobile accessories.', 'https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=900&q=80'),
            ('Laptops', 'laptops', 'Work, gaming, creator, and study-ready portable computers.', 'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=900&q=80'),
            ('Audio', 'audio', 'Headphones, speakers, earbuds, and studio sound gear.', 'https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=900&q=80'),
            ('Fashion', 'fashion', 'Everyday clothing, shoes, watches, and style essentials.', 'https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=900&q=80'),
            ('Home', 'home', 'Smart home, kitchen, decor, and comfort upgrades.', 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?auto=format&fit=crop&w=900&q=80'),
            ('Fitness', 'fitness', 'Wearables, workout accessories, and recovery products.', 'https://images.unsplash.com/photo-1517838277536-f5f99be501cd?auto=format&fit=crop&w=900&q=80');
            """;

        await ExecuteAsync(connection, sql);
    }

    private static async Task SeedProductsAsync(MySqlConnection connection)
    {
        if (await CountAsync(connection, "products") > 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO products (name, description, price, image_url, stock, category_id, rating, review_count, brand, tags, featured) VALUES
            ('Nova X1 5G Smartphone', 'A fast 5G phone with a bright AMOLED display, night camera, and all-day battery.', 599.99, 'https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=900&q=80', 24, 1, 4.7, 318, 'Nova', 'phone,5g,camera,android,amoled', TRUE),
            ('PixelPro Fold Mini', 'Compact foldable phone for multitasking, streaming, and pocket-friendly productivity.', 1099.00, 'https://images.unsplash.com/photo-1616348436168-de43ad0db179?auto=format&fit=crop&w=900&q=80', 9, 1, 4.6, 142, 'PixelPro', 'phone,foldable,5g,camera,premium', TRUE),
            ('AeroCharge Wireless Pad', 'Qi-compatible fast wireless charger with cooling vents and a soft-touch top.', 49.99, 'https://images.unsplash.com/photo-1615529328331-f8917597711f?auto=format&fit=crop&w=900&q=80', 82, 1, 4.4, 201, 'Aero', 'charger,wireless,phone,accessory,fast', FALSE),
            ('ShieldCase Clear Armor', 'Slim transparent phone case with raised edges and reinforced corners.', 24.99, 'https://images.unsplash.com/photo-1601593346740-925612772716?auto=format&fit=crop&w=900&q=80', 120, 1, 4.2, 88, 'ShieldCase', 'case,phone,accessory,clear,protection', FALSE),
            ('ZenBook Air 14', 'Lightweight laptop with a 14-inch display, long battery life, and quiet performance.', 899.00, 'https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=900&q=80', 16, 2, 4.8, 436, 'ZenBook', 'laptop,work,student,lightweight,ssd', TRUE),
            ('Raptor Gaming 16', 'High-refresh gaming laptop with RTX graphics, RGB keyboard, and advanced cooling.', 1499.00, 'https://images.unsplash.com/photo-1603302576837-37561b2e2302?auto=format&fit=crop&w=900&q=80', 7, 2, 4.7, 194, 'Raptor', 'laptop,gaming,rtx,rgb,performance', TRUE),
            ('CreatorDock USB-C Hub', 'Eight-port USB-C hub for monitors, SD cards, ethernet, and fast pass-through charging.', 89.99, 'https://images.unsplash.com/photo-1625842268584-8f3296236761?auto=format&fit=crop&w=900&q=80', 54, 2, 4.5, 167, 'CreatorDock', 'laptop,hub,usb-c,accessory,creator', FALSE),
            ('QuietKeys Mechanical Keyboard', 'Compact wireless mechanical keyboard with hot-swap switches and soft lighting.', 119.99, 'https://images.unsplash.com/photo-1618384887929-16ec33fab9ef?auto=format&fit=crop&w=900&q=80', 38, 2, 4.6, 231, 'QuietKeys', 'keyboard,laptop,wireless,mechanical,desk', FALSE),
            ('PulseBeat ANC Headphones', 'Over-ear headphones with active noise cancellation and warm, detailed sound.', 199.99, 'https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=900&q=80', 31, 3, 4.8, 512, 'PulseBeat', 'headphones,audio,anc,wireless,music', TRUE),
            ('PocketSound Mini Speaker', 'Water-resistant Bluetooth speaker with big bass in a small travel-ready body.', 79.99, 'https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=900&q=80', 46, 3, 4.5, 292, 'PocketSound', 'speaker,audio,bluetooth,portable,bass', FALSE),
            ('AirBuds Studio Pro', 'Wireless earbuds with low-latency gaming mode, ANC, and a pocket charging case.', 129.99, 'https://images.unsplash.com/photo-1606220588913-b3aacb4d2f46?auto=format&fit=crop&w=900&q=80', 64, 3, 4.6, 377, 'AirBuds', 'earbuds,audio,anc,wireless,phone', TRUE),
            ('StudioMic USB', 'Plug-and-play microphone for streaming, meetings, podcasts, and voiceovers.', 99.99, 'https://images.unsplash.com/photo-1590602847861-f357a9332bbc?auto=format&fit=crop&w=900&q=80', 19, 3, 4.4, 119, 'StudioMic', 'microphone,audio,usb,creator,streaming', FALSE),
            ('UrbanTrail Sneakers', 'Breathable everyday sneakers with cushioned soles and a clean streetwear profile.', 84.99, 'https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=900&q=80', 58, 4, 4.5, 248, 'UrbanTrail', 'shoes,sneakers,fashion,comfort,streetwear', TRUE),
            ('MetroFlex Jacket', 'Lightweight weather-resistant jacket with stretch panels and hidden pockets.', 119.00, 'https://images.unsplash.com/photo-1523398002811-999ca8dec234?auto=format&fit=crop&w=900&q=80', 34, 4, 4.3, 92, 'MetroFlex', 'jacket,fashion,outerwear,travel,water-resistant', FALSE),
            ('Classic Chrono Watch', 'Minimal steel watch with a leather strap and practical chronograph details.', 159.00, 'https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=900&q=80', 27, 4, 4.7, 171, 'Classic', 'watch,fashion,accessory,leather,steel', TRUE),
            ('DailyFit Backpack', 'Organized backpack with laptop sleeve, bottle pocket, and commuter-friendly padding.', 69.99, 'https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=900&q=80', 49, 4, 4.4, 138, 'DailyFit', 'bag,fashion,laptop,travel,commute', FALSE),
            ('BrewMaster Smart Kettle', 'Temperature-controlled kettle with presets for tea, coffee, and pour-over routines.', 89.99, 'https://images.unsplash.com/photo-1544787219-7f47ccb76574?auto=format&fit=crop&w=900&q=80', 23, 5, 4.6, 183, 'BrewMaster', 'kettle,home,kitchen,smart,coffee', TRUE),
            ('GlowNest Table Lamp', 'Adjustable warm-to-cool desk lamp with touch controls and a modern metal base.', 64.99, 'https://images.unsplash.com/photo-1507473885765-e6ed057f782c?auto=format&fit=crop&w=900&q=80', 44, 5, 4.4, 76, 'GlowNest', 'lamp,home,desk,lighting,decor', FALSE),
            ('CleanBot Mini Vacuum', 'Compact robot vacuum for daily floor maintenance and app-guided room cleaning.', 249.00, 'https://images.unsplash.com/photo-1600369672770-985fd30004eb?auto=format&fit=crop&w=900&q=80', 15, 5, 4.5, 203, 'CleanBot', 'vacuum,home,smart,cleaning,robot', TRUE),
            ('CozyWeave Throw Blanket', 'Soft woven throw blanket for sofa styling, reading corners, and cold evenings.', 39.99, 'https://images.unsplash.com/photo-1616046229478-9901c5536a45?auto=format&fit=crop&w=900&q=80', 73, 5, 4.2, 61, 'CozyWeave', 'blanket,home,decor,soft,comfort', FALSE),
            ('FitPulse Smart Band', 'Slim fitness tracker with heart rate monitoring, sleep insights, and workout modes.', 59.99, 'https://images.unsplash.com/photo-1576243345690-4e4b79b63288?auto=format&fit=crop&w=900&q=80', 57, 6, 4.5, 254, 'FitPulse', 'fitness,wearable,tracker,health,smart', TRUE),
            ('GripMax Yoga Mat', 'Non-slip yoga mat with joint-friendly cushioning and alignment guide marks.', 34.99, 'https://images.unsplash.com/photo-1601925260368-ae2f83cf8b7f?auto=format&fit=crop&w=900&q=80', 90, 6, 4.6, 188, 'GripMax', 'fitness,yoga,mat,workout,stretch', FALSE),
            ('HydroRun Steel Bottle', 'Insulated bottle that keeps drinks cold during training, commuting, and hikes.', 29.99, 'https://images.unsplash.com/photo-1602143407151-7111542de6e8?auto=format&fit=crop&w=900&q=80', 110, 6, 4.3, 132, 'HydroRun', 'fitness,bottle,hydration,steel,travel', FALSE),
            ('RecoverPro Massage Gun', 'Percussive recovery tool with quiet motor, multiple heads, and travel case.', 139.99, 'https://images.unsplash.com/photo-1627400713572-6c29f188ba7d?auto=format&fit=crop&w=900&q=80', 21, 6, 4.7, 221, 'RecoverPro', 'fitness,recovery,massage,workout,muscle', TRUE);
            """;

        await ExecuteAsync(connection, sql);
    }

    private async Task SeedAdminUserAsync(MySqlConnection connection)
    {
        if (await CountAsync(connection, "users") > 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (full_name, email, password_hash, role)
            VALUES (@FullName, @Email, @PasswordHash, 'Admin');
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

        CREATE TABLE IF NOT EXISTS users (
            id INT AUTO_INCREMENT PRIMARY KEY,
            full_name VARCHAR(180) NOT NULL,
            email VARCHAR(180) NOT NULL UNIQUE,
            password_hash VARCHAR(500) NOT NULL,
            role VARCHAR(40) NOT NULL DEFAULT 'Customer',
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE TABLE IF NOT EXISTS orders (
            id INT AUTO_INCREMENT PRIMARY KEY,
            user_id INT NULL,
            full_name VARCHAR(180) NOT NULL,
            email VARCHAR(180) NOT NULL,
            shipping_address VARCHAR(700) NOT NULL,
            total DECIMAL(10, 2) NOT NULL,
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
        """;
}
