# AuthDemo — .NET 10 JWT Auth (Minimal API + Controller)

## Quick start

```bash
dotnet run
# Swagger/Scalar UI → https://localhost:5001/scalar
```

---

## Endpoints

### Auth (public)
| Method | URL | Body |
|--------|-----|------|
| POST | `/api/auth/login` | `{ "username": "admin", "password": "admin123" }` |

**Built-in users**

| Username | Password | Roles |
|----------|----------|-------|
| admin | admin123 | Admin, User |
| alice | alice123 | User |
| bob | bob123 | User, Manager |

---

### Products — Minimal API  (`/api/products`)

| Method | URL | Required role |
|--------|-----|---------------|
| GET | `/api/products` | Any authenticated |
| GET | `/api/products/{id}` | Any authenticated |
| POST | `/api/products` | Manager **or** Admin |
| DELETE | `/api/products/{id}` | Admin |

---

### Weather — Controller API  (`/api/weather`)

| Method | URL | Required role |
|--------|-----|---------------|
| GET | `/api/weather` | Any authenticated |
| GET | `/api/weather/me` | Any authenticated |
| GET | `/api/weather/admin` | Admin |

---

## Usage with curl

```bash
# 1. Login — get token
TOKEN=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' \
  | jq -r '.token')

# 2. Use the token
curl -s https://localhost:5001/api/weather \
  -H "Authorization: Bearer $TOKEN" | jq

curl -s https://localhost:5001/api/weather/admin \
  -H "Authorization: Bearer $TOKEN" | jq

curl -s https://localhost:5001/api/products \
  -H "Authorization: Bearer $TOKEN" | jq

# 3. Test role restriction — alice has no Manager/Admin role
ALICE=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"alice123"}' | jq -r '.token')

# → 403 Forbidden
curl -s -X POST https://localhost:5001/api/products \
  -H "Authorization: Bearer $ALICE" \
  -H "Content-Type: application/json" \
  -d '{"id":0,"name":"Pen","price":1.99,"category":"Stationery"}' | jq
```

---

## Architecture notes

```
Program.cs
  ├── JWT Bearer authentication (Microsoft.AspNetCore.Authentication.JwtBearer)
  ├── Authorization middleware
  │
  ├── /api/auth/login          — Minimal API, anonymous, issues JWT
  │
  ├── /api/products/**         — Minimal API group, RequireAuthorization()
  │     ProductEndpoints.cs    — per-endpoint [Authorize(Roles="...")] attributes
  │
  └── /api/weather/**          — Controller (WeatherController)
        [Authorize] on class   — default: any authenticated user
        [Authorize(Roles=...)] — per-action role restriction
```

### Key decisions

- **`UseAuthentication()` before `UseAuthorization()`** — order matters in the pipeline.
- **`ClockSkew = TimeSpan.Zero`** — tokens expire exactly when they say, no grace window.
- **Custom `OnChallenge` / `OnForbidden`** — returns JSON instead of empty 401/403.
- **`AllowAnonymous()`** on the login endpoint — overrides any global `RequireAuthorization`.
- **Scalar** replaces Swagger UI (built-in .NET 10 OpenAPI support via `AddOpenApi()`).