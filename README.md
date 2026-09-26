# AuthDemo — .NET 10 JWT Auth (Minimal API + Controller)

## Quick start

```bash
dotnet run
# Scalar UI → https://localhost:5001/scalar
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

### Health

| Method | URL | Auth | Purpose |
|--------|-----|------|---------|
| GET | `/health` | Admin JWT | Full report — all checks, durations, exceptions |
| GET | `/health/ready` | Anonymous | Readiness probe — DB + external API |
| GET | `/health/live` | Anonymous | Liveness probe — process self-check only |

**Tag → endpoint mapping**

| Tag | Checks included | Endpoint |
|-----|----------------|----------|
| `live` | `self` | `/health/live` |
| `ready` | `self`, `database`, `external-api` | `/health/ready` |
| *(none)* | all | `/health` |

**Status → HTTP code**

| Status | `/health` | `/health/ready` | `/health/live` |
|--------|-----------|-----------------|----------------|
| Healthy | 200 | 200 | 200 |
| Degraded | 200 | 200 | 200 |
| Unhealthy | 503 | 503 | 503 |

---

### Products — Minimal API (`/api/products`)

| Method | URL | Required role |
|--------|-----|---------------|
| GET | `/api/products` | Any authenticated |
| GET | `/api/products/{id}` | Any authenticated |
| POST | `/api/products` | Manager **or** Admin |
| DELETE | `/api/products/{id}` | Admin |

---

### Weather — Controller API (`/api/weather`)

| Method | URL | Required role |
|--------|-----|---------------|
| GET | `/api/weather` | Any authenticated |
| GET | `/api/weather/me` | Any authenticated |
| GET | `/api/weather/admin` | Admin |

---

## Usage with curl

```bash
# ── Probes (no token needed) ──────────────────────────────────────────────────
curl https://localhost:5001/health/live  | jq
curl https://localhost:5001/health/ready | jq

# ── Full health report (Admin JWT required) ───────────────────────────────────
TOKEN=$(curl -s -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

curl https://localhost:5001/health \
  -H "Authorization: Bearer $TOKEN" | jq

# ── Other protected endpoints ─────────────────────────────────────────────────
curl https://localhost:5001/api/weather \
  -H "Authorization: Bearer $TOKEN" | jq

curl https://localhost:5001/api/products \
  -H "Authorization: Bearer $TOKEN" | jq
```

---

## Kubernetes probe config

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 15
  failureThreshold: 3
```

---

## Architecture notes

```
Program.cs
  ├── JWT Bearer authentication
  ├── Authorization — AdminOnly policy (used by /health)
  ├── Health checks registered with tags
  │
  ├── /health          — full detail, RequireAuthorization("AdminOnly")
  ├── /health/ready    — "ready" tagged checks, AllowAnonymous
  ├── /health/live     — "live" tagged checks, AllowAnonymous
  │
  ├── /api/auth/login  — Minimal API, anonymous, issues JWT
  ├── /api/products/** — Minimal API group, role-restricted
  └── /api/weather/**  — Controller, role-restricted
```

### Key decisions

- **`/health` is protected** — exposes internal dependency names/errors; restrict to ops tooling.
- **`/health/ready` vs `/health/live`** — different probe semantics. Liveness only cares if the process is up; readiness also checks external dependencies. Keep them separate so a slow DB doesn't cause unnecessary pod restarts.
- **`Degraded` returns 200 on probes** — a degraded external API means reduced functionality, not a hard failure; the pod stays in the load-balancer rotation.
- **`failureStatus: HealthStatus.Degraded`** on `ExternalApiHealthCheck` — upstream flakiness degrades instead of marking the whole app unhealthy.
- **`UseAuthentication()` before `UseAuthorization()`** — order matters in the pipeline.
- **`ClockSkew = TimeSpan.Zero`** — tokens expire exactly when they say, no grace window.