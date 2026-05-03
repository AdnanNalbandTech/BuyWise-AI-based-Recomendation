CREATE DATABASE IF NOT EXISTS buywise_db CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE buywise_db;

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

INSERT INTO categories (name, slug, description, image_url)
SELECT * FROM (
    SELECT 'Electronics' AS name, 'electronics' AS slug, 'Smart gadgets, cameras, tablets, and creator tech.' AS description, 'https://images.unsplash.com/photo-1550009158-9ebf69173e03?auto=format&fit=crop&w=900&q=80' AS image_url
    UNION ALL SELECT 'Mobiles', 'mobiles', '5G phones, cases, chargers, and mobile accessories.', 'https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=900&q=80'
    UNION ALL SELECT 'Laptops', 'laptops', 'Work, gaming, creator, and study-ready portable computers.', 'https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=900&q=80'
    UNION ALL SELECT 'Shoes', 'shoes', 'Running, training, casual, and lifestyle footwear.', 'https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=900&q=80'
    UNION ALL SELECT 'Clothes', 'clothes', 'Everyday apparel, outerwear, activewear, and essentials.', 'https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=900&q=80'
    UNION ALL SELECT 'Watches', 'watches', 'Smartwatches, analog watches, and premium timepieces.', 'https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=900&q=80'
    UNION ALL SELECT 'Home Appliances', 'home-appliances', 'Kitchen, cleaning, comfort, and smart home appliances.', 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?auto=format&fit=crop&w=900&q=80'
    UNION ALL SELECT 'Accessories', 'accessories', 'Bags, chargers, keyboards, headphones, cases, and add-ons.', 'https://images.unsplash.com/photo-1553062407-98eeb64c6a62?auto=format&fit=crop&w=900&q=80'
) seed
WHERE NOT EXISTS (SELECT 1 FROM categories WHERE categories.slug = seed.slug);

INSERT INTO faqs (question, answer, keywords)
SELECT * FROM (
    SELECT 'What is the return policy?' AS question, 'Most BUYWISE products can be returned within 7 days of delivery if unused, with original packaging and invoice.' AS answer, 'return,refund,policy,exchange' AS keywords
    UNION ALL SELECT 'How long does delivery take?', 'Standard delivery usually takes 3 to 6 business days. Metro cities may receive eligible products in 1 to 3 business days.', 'delivery,shipping,time,courier'
    UNION ALL SELECT 'Which payment methods are supported?', 'BUYWISE supports UPI, debit cards, credit cards, net banking, wallets, and cash on delivery for eligible pin codes.', 'payment,upi,card,cod,wallet'
    UNION ALL SELECT 'How does warranty work?', 'Warranty is provided by the product brand or seller and support is available through service centers or BUYWISE support.', 'warranty,guarantee,service,repair'
    UNION ALL SELECT 'Can I cancel my order?', 'Orders can be cancelled while they are Pending or Processing. Once shipped, use return support after delivery if eligible.', 'cancel,cancellation,order,status'
) seed
WHERE NOT EXISTS (SELECT 1 FROM faqs WHERE faqs.question = seed.question);

-- The ASP.NET Core DatabaseInitializer seeds the full production-style product catalog,
-- product_tags, admin user, and frequently_bought_together rows idempotently on API startup.
