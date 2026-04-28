# BUYWISE

BUYWISE is a full-stack ecommerce starter built with:

- ASP.NET Core Web API (`backend/BuyWise.Api`)
- Angular standalone frontend (`frontend/buywise-client`)
- MySQL Server database (`buywise_db`)

The application starts at login/register, then opens a responsive storefront with product categories, product details, cart, checkout, admin product management, and an AI-style recommendation endpoint.

## Local Requirement Check

Checked in this workspace:

- .NET SDK: `10.0.201`
- Node: `v24.14.1`
- npm: available as `npm.cmd` (`11.11.0`)
- MySQL Server client: `8.0.46`
- Angular CLI: not installed globally, so this project uses local Angular CLI from `package.json`
- Git: not available on PATH

PowerShell script execution blocks `npm.ps1`, so use `npm.cmd` in this folder.

## Default Accounts

The API seeds an admin user on first startup:

- Email: `admin@buywise.local`
- Password: `Admin@12345`

Newly registered users are customers. Admin-only product create/update/delete calls require the admin token.

## Database

Default connection string is in `backend/BuyWise.Api/appsettings.json`:

```json
"Server=localhost;Port=3306;Database=buywise_db;User ID=root;Password=;Allow User Variables=True;"
```

Update the MySQL username/password before running if your local root account has a password. The API creates the database and seeds products automatically. The standalone SQL schema is also available at `database/schema.sql`.

## Run the Backend

```powershell
cd "New project\dotnetproject"
$env:DOTNET_CLI_HOME="$PWD\.dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT="1"
dotnet restore backend\BuyWise.Api\BuyWise.Api.csproj
dotnet run --project backend\BuyWise.Api\BuyWise.Api.csproj
```

API URL: `http://localhost:5148`

Useful endpoints:

- `GET /api/health`
- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET /api/categories`
- `GET /api/products`
- `GET /api/products/{id}`
- `GET /api/recommendations/{productId}?cartIds=1,2,3`
- `POST /api/orders`

## Run the Frontend

```powershell
cd "New project\dotnetproject\frontend\buywise-client"
npm.cmd install
npm.cmd start
```

Angular URL: `http://localhost:4200`

## Recommendation Feature

The backend recommendation service is intentionally local and transparent for coursework/demo use. It scores related products using:

- Same product category
- Shared AI tags
- Same brand
- Similar price range
- Products already in the cart
- Rating, reviews, and featured status

This means recommendations change when a product is opened from detail pages and when the cart contains related products.
