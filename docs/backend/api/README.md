# LiveSync.Api - Authentication Service

## Overview
This is a standalone authentication API service for the LiveSync application. It provides JWT-based authentication with support for user registration, login, and OAuth integration (planned).

## Features

### ? Implemented
- User registration with email and password
- Login with email or username
- JWT token generation and validation
- Password hashing and security
- User lockout after failed attempts
- Identity integration with ASP.NET Core
- Swagger documentation with JWT authorization
- CORS support for cross-origin requests

### ?? Planned (OAuth)
- Google OAuth login
- GitHub OAuth login
- Microsoft OAuth login

## API Endpoints

All endpoints are prefixed with `/api/auth`

### 1. Register
**POST** `/api/auth/register`

Register a new user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "Password123",
  "confirmPassword": "Password123",
  "firstName": "John",
  "lastName": "Doe"
}
```

### 2. Login
**POST** `/api/auth/login`

Login with email or username and password.

**Request Body:**
```json
{
  "emailOrUsername": "user@example.com",
  "password": "Password123",
  "rememberMe": false
}
```

### 3. Get Current User
**GET** `/api/auth/me`

Get information about the currently authenticated user (requires Bearer token).

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

### 4. Refresh Token
**POST** `/api/auth/refresh`

Refresh an expired JWT token (not yet fully implemented).

### 5. OAuth Endpoints (Planned)
- **POST** `/api/auth/oauth/google` - Google OAuth
- **POST** `/api/auth/oauth/github` - GitHub OAuth
- **POST** `/api/auth/oauth/microsoft` - Microsoft OAuth

## Configuration

### Required settings

Configure secrets with environment variables or user-secrets instead of committing them to `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=livesync;Username=devuser;Password=devpassword"
  },
  "Database": {
    "EnsureCreatedOnStartup": true,
    "MaintenanceDatabase": "postgres",
    "MigrateOnStartup": true
  },
  "Jwt": {
    "Secret": "YourSuperSecretKeyForJWT_ChangeThisInProduction_32Characters!",
    "Issuer": "LiveSyncAuthAPI",
    "Audience": "LiveSyncClient",
    "ExpirationHours": 24
  }
}
```

**Important:** `Jwt:Secret` is required at startup and must be at least 32 bytes. Use a strong production value.

## Database

The API now uses **PostgreSQL** through Npgsql/Entity Framework Core.

For local development, point the API at the shared `postgres-dev` server. The
connection string targets LiveSync's own database, `livesync`; startup connects
to the maintenance database, `postgres`, creates `livesync` when missing, and
then applies all EF migrations.

For production cutover notes, environment variables, and existing SQL Server data migration caveats, see `../../POSTGRESQL_MIGRATION.md`.

## Running the Service

### Development
```bash
cd LiveSync.Api
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7001` (or as configured in launchSettings.json)
- HTTP: `http://localhost:5001`

### Testing with Swagger
1. Start the application
2. Navigate to `https://localhost:7001/swagger`
3. Register a new user using `/api/auth/register`
4. Copy the token from the response
5. Click the "Authorize" button at the top of Swagger UI
6. Enter: `Bearer <your-token>`
7. Test protected endpoints like `/api/auth/me`

## Security Features

### Password Requirements
- Minimum length: 6 characters
- Requires at least one digit
- Requires at least one lowercase letter
- Requires at least one uppercase letter

### Account Lockout
- Max failed attempts: 5
- Lockout duration: 5 minutes

## Integration with Other Services

This authentication API can be consumed by:
- **LiveSync.SignalR** - The main SignalR hub service
- **Frontend Applications** - Angular, React, Vue, etc.
- **Mobile Applications** - iOS, Android

### Example: Configuring SignalR Service to Use This Auth API

In your SignalR service, configure JWT validation to accept tokens from this service:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "LiveSyncAuthAPI",
            ValidAudience = "LiveSyncClient",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("YourSuperSecretKeyForJWT_ChangeThisInProduction_32Characters!"))
        };
    });
```

## Project Structure

```
LiveSync.Api/
??? Controllers/
?   ??? AuthController.cs       # API endpoints
??? Services/
?   ??? IAuthService.cs         # Service interface
?   ??? AuthService.cs          # Authentication logic
??? Models/
?   ??? ApplicationUser.cs      # User entity
??? DTOs/
?   ??? AuthDTOs.cs            # Request/Response models
??? Data/
?   ??? ApplicationDbContext.cs # EF Core context
??? Program.cs                  # Application setup
??? appsettings.json           # Configuration
```

## Dependencies

- `Microsoft.AspNetCore.Authentication.JwtBearer` - JWT authentication
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` - Identity framework
- `Microsoft.EntityFrameworkCore.InMemory` - In-memory database (dev only)
- `Swashbuckle.AspNetCore` - Swagger/OpenAPI support

## Notes
- All endpoints return standardized `AuthResponse` objects
- JWT tokens expire after 24 hours (configurable)
- User passwords are hashed using ASP.NET Core Identity
- The in-memory database resets on application restart
- CORS is configured to allow all origins in development (configure appropriately for production)
