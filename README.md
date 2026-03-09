# TripNow - Sistema de Reservas con Evaluacion de Riesgo

Backend en `.NET 8` con `Minimal API`, evaluacion de riesgo asincrona, PostgreSQL y tolerancia a latencia/fallos del proveedor externo.

## Arquitectura

```
TripNow.Domain           -> Entidad Reservation, estados, trazabilidad (ReservationDecision)
TripNow.Application      -> Casos de uso, validaciones, puertos (IReservationRepository, IRiskProviderClient)
TripNow.Infrastructure   -> EF Core + PostgreSQL, cliente HTTP de riesgo, background worker (DB polling)
TripNow.Api              -> Endpoints Minimal API, middleware de excepciones, health checks
TripNow.UnitTests        -> 5 tests unitarios del servicio de reservas
TripNow.IntegrationTests -> 5 tests de integracion HTTP (InMemory DB)
```

### Patrones aplicados

- **Hexagonal (Ports & Adapters)**: logica de negocio aislada de frameworks e infraestructura.
- **Background Worker con DB polling**: desacopla creacion de reserva (< 500ms) de evaluacion de riesgo.
- **Fail-safe policy**: si el proveedor falla, se rechaza alta exposicion y se aprueba baja exposicion.
- **Modelo de dominio rico**: factory methods y transiciones de estado controladas en la entidad.

### Stack tecnologico

- .NET 8 con Minimal API
- Entity Framework Core 8 + PostgreSQL (Npgsql)
- `TimeProvider` (.NET 8 built-in) para abstraccion de tiempo testeable
- `BackgroundService` con DB polling para procesamiento asincrono de riesgo
- Docker + Docker Compose

## Ejecucion local

### Prerequisitos

- .NET 8 SDK
- PostgreSQL (o Docker)

### Con Docker Compose (recomendado)

```bash
docker compose up -d
```

Levanta PostgreSQL 16 + la API. Disponible en `http://localhost:8080`.

### Sin Docker

1. Tener PostgreSQL corriendo en `localhost:5432` con usuario `tripnow` / password `tripnow`.
2. Ejecutar:

```bash
dotnet restore TripNow.slnx
dotnet run --project TripNow.Api
```

API en `http://localhost:5044`. Swagger UI en desarrollo: `http://localhost:5044/swagger`.

## Tests

```bash
dotnet test TripNow.slnx
```

- **5 unit tests**: validaciones, rechazo por pais, fallback alta/baja exposicion, limite pendientes.
- **5 integration tests**: crear + obtener, bad request, listar, not found, rechazo por pais.

Los integration tests usan `InMemoryDatabase` (no requieren PostgreSQL).

## Docker

### Build manual

```bash
docker build -t tripnow-api .
```

### Docker Compose

```bash
docker compose up -d        # levantar
docker compose down          # detener
docker compose down -v       # detener y borrar volumen de datos
```

Health check: `GET http://localhost:8080/health`

---

## Endpoints

### POST /reservations

Crea una reserva. Responde en < 500ms. La evaluacion de riesgo se procesa en background.

**Ejemplo 1 - Reserva basica (baja exposicion):**

```bash
curl -X POST http://localhost:8080/reservations \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "carlos@ejemplo.com",
    "tripCountry": "MX",
    "amount": 1500.00,
    "currency": "USD",
    "tripStartDate": "2026-05-15T10:00:00Z"
  }'
```

```json
{
  "reservationId": "a1b2c3d4-...",
  "customerEmail": "carlos@ejemplo.com",
  "tripCountry": "MX",
  "amount": 1500.00,
  "currency": "USD",
  "tripStartDate": "2026-05-15T10:00:00+00:00",
  "status": "PendingRiskCheck",
  "createdAtUtc": "2026-03-08T20:00:00+00:00",
  "riskEvaluatedAtUtc": null,
  "finalizedAtUtc": null,
  "risk": {
    "evaluationStatus": "Unknown",
    "riskScore": null,
    "providerStatus": null,
    "providerError": null
  },
  "decisions": [
    {
      "timestampUtc": "2026-03-08T20:00:00+00:00",
      "reason": "Reservation created and queued for risk evaluation.",
      "resultingStatus": "PendingRiskCheck"
    }
  ]
}
```

**Ejemplo 2 - Alta exposicion (amount >= 5000):**

```bash
curl -X POST http://localhost:8080/reservations \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "maria@ejemplo.com",
    "tripCountry": "US",
    "amount": 8000.00,
    "currency": "USD",
    "tripStartDate": "2026-06-01T08:00:00Z"
  }'
```

Si el proveedor de riesgo falla, esta reserva sera `REJECTED` (no puede aprobarse sin evaluacion positiva).

**Ejemplo 3 - Pais no soportado (rechazo inmediato):**

```bash
curl -X POST http://localhost:8080/reservations \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "test@ejemplo.com",
    "tripCountry": "ZZ",
    "amount": 500,
    "currency": "USD",
    "tripStartDate": "2026-05-15T10:00:00Z"
  }'
```

Responde `202 Accepted` con `status: "Rejected"` y decision `"Trip country is not supported."`.

**Errores de validacion (400 Bad Request):**

```bash
# Monto invalido
curl -X POST http://localhost:8080/reservations \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"a@b.com","tripCountry":"MX","amount":0,"currency":"USD","tripStartDate":"2026-05-15T10:00:00Z"}'

# Fecha muy cercana (< 48h)
curl -X POST http://localhost:8080/reservations \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"a@b.com","tripCountry":"MX","amount":100,"currency":"USD","tripStartDate":"2026-03-09T10:00:00Z"}'

# Moneda no soportada
curl -X POST http://localhost:8080/reservations \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"a@b.com","tripCountry":"MX","amount":100,"currency":"GBP","tripStartDate":"2026-05-15T10:00:00Z"}'
```

### GET /reservations/{reservationId}

Obtiene una reserva por ID con detalle de riesgo y decisiones.

```bash
curl http://localhost:8080/reservations/a1b2c3d4-5678-90ab-cdef-1234567890ab
```

- `200 OK` con el detalle completo (campos de riesgo poblados tras evaluacion).
- `404 Not Found` si el ID no existe.

### GET /reservations

Lista todas las reservas ordenadas por fecha de creacion (descendente).

```bash
curl http://localhost:8080/reservations
```

### GET /health

Health check del servicio.

```bash
curl http://localhost:8080/health
```

---

## Reglas de negocio

| # | Regla | Descripcion |
|---|-------|-------------|
| 1 | Monto valido | `amount > 0` y `amount <= 20000` |
| 2 | Fecha valida | `tripStartDate` no en pasado, al menos 48h adelante |
| 3 | Pais soportado | `tripCountry` en lista configurable. Si no -> `REJECTED` |
| 4 | Moneda soportada | `currency` en lista configurable |
| 5 | Limite reservas activas | Max 3 `PENDING_RISK_CHECK` por cliente en 24h. Si excede -> `REJECTED` |
| 6 | Alta exposicion | `amount >= 5000` o pais en `HighRiskCountries` -> requiere evaluacion positiva |
| 7 | Trazabilidad | Cada cambio de estado queda registrado con timestamp y razon |

## Configuracion

Todos los parametros son configurables via `appsettings.json`:

## Manejo de latencia y fallos del proveedor

- `POST /reservations` **no llama** al proveedor de forma sincrona. Responde < 500ms.
- Un `BackgroundService` poll la DB cada 2 segundos buscando reservas `PENDING_RISK_CHECK` y las procesa en batch.
- El cliente HTTP tiene **timeout por intento** (1200ms) y **reintentos con backoff exponencial** (100ms, 200ms, 400ms).
- Si el proveedor falla definitivamente:

| Escenario | Resultado |
|-----------|-----------|
| Proveedor OK + APPROVED | `APPROVED` |
| Proveedor OK + REJECTED | `REJECTED` |
| Proveedor falla + alta exposicion | `REJECTED` |
| Proveedor falla + baja exposicion | `APPROVED` (continuidad operativa) |

## Decisiones tecnicas

- **PostgreSQL** sobre SQLite: soporte completo de LINQ, indices funcionales, concurrencia real.
- **DB polling** sobre cola in-memory: sobrevive restarts, no pierde mensajes, mas simple que un broker externo.
- **`TimeProvider`** (.NET 8) sobre `IClock` custom: abstraccion estandar del framework, sin codigo custom innecesario.
- **Minimal API** sobre Controllers: menor overhead, arranque rapido, alineado a serverless.
- **Hexagonal**: facilita testing con fakes y migracion de infraestructura sin tocar logica de negocio.
