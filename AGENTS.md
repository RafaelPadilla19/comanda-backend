# AGENTS.md — Comanda.Backend

API de **Comanda**, plataforma multi-tenant de gestión para restaurantes (El Salvador).

## Stack

- .NET 10, **FastEndpoints** (arquitectura vertical-slice, no controllers clásicos)
- EF Core 10 + Npgsql, PostgreSQL dedicado en **Neon** (no Supabase, no Cloud SQL)
- Deploy: **Cloud Run** (GCP, proyecto `comanda-501203`) + Secret Manager
- Corre local en `:5057`

## Estructura

Cada feature vive en `src/Comanda.Api/Features/<Nombre>/` como vertical slice: request/response DTOs, validator, endpoint, todo junto en el mismo archivo o carpeta. No hay capa de controllers separada.

```
src/
  Comanda.Domain/         entidades, abstracciones (interfaces de repos), enums
  Comanda.Infrastructure/ EF Core, repos, servicios concretos, migrations
  Comanda.Api/Features/   endpoints FastEndpoints por feature
```

## Dominios clave (para orientarse rápido)

- **Multi-tenant** (`Features/Settings`, `TenantProvisioningService`): `Tenant` NO es `ITenantScoped` (es la raíz). `Tenant.Currency/CurrencySymbol/TaxRate` son dinámicos por restaurante (se precargan según el país al registrarse, editables en Configuración > Negocio vía `/settings/business`). Los precios del menú asumen "impuesto incluido" (estilo SV); el modo "agregado" (sales tax USA) sigue sin implementar.
- **Países — catálogo DINÁMICO** (`Comanda.Domain.Entities.Country`, tabla `Countries`): reemplaza la vieja lista estática `SupportedCountries`. **Nunca volver a hardcodear una lista de países** — el operador los agrega/edita/desactiva en `/platform/countries` (CRUD, super-admin) sin redeploy. Lecturas desde código van por `Comanda.Infrastructure.Persistence.CountryCatalog` (`IsValidAsync`/`DefaultsForAsync`/`ActiveCodesAsync`), nunca por un array local. Endpoint público `GET /countries` (sin sesión) expone los activos para selects (registro, Configuración, Pagos). Seed inicial en `DbSeeder.EnsureCountriesAsync` (SV/GT/HN/NI/CR/PA/CO), solo agrega los que falten — el operador puede editarlos después sin que el seed los pise.
- **Control-Plane / super-admin** (`Features/Platform/PlatformEndpoints.cs`, rol `PlatformAdmin`, `IgnoreQueryFilters` para cruzar tenants): CRUD de planes y países, catálogo de métodos de pago por país, credenciales Wompi de plataforma, y **ficha de tenant** (`GET /platform/tenants/{id}`) con analítica (ingresos, ticket promedio, productos más vendidos, sucursales, usuarios, historial de pagos) + acciones (asignar plan, suspender/activar, `PUT /platform/tenants/{id}/subscription` para extender manualmente el vencimiento — ajuste administrativo, NO crea un `SubscriptionPayment`).
- **Inventario** (`Features/Inventory`, `Features/Catalog`): **Kardex** = tabla `InventoryMovement` (ledger real, `Reason` enum Entrada/Salida/Merma/Ajuste/Venta) junto a `InventoryItem.Stock`; cualquier ajuste manual exige motivo+nota. **Receta/BOM** = `Product.Recipe: List<RecipeLine>`, value object **owned-JSON** (`OwnsMany(...).ToJson()`, no tabla propia — es config, no un ledger). **Descuento automático**: al mover un pedido A `Entregados` (`OrderEndpoints.MoveOrderEndpoint`, guardado contra re-moves) se consume la receta de cada línea, agregando por insumo antes de escribir, y se registra un `InventoryMovement` con `Reason.Venta`. Todo esto está gateado al plan **Elite** (`PlanFeature.Inventory`), mismo flag para Inventario/Kardex/Receta.

## Reglas de código

1. **Idioma**: código y mensajes de commit en español. Mensajes de error al usuario en español (`{codigo, mensaje}`), nunca inglés.
2. **Multi-tenant — CRÍTICO**: cualquier entidad que implemente `ITenantScoped` tiene un `HasQueryFilter` global por `TenantId` (ver `ComandaDbContext.OnModelCreating`). En **cualquier endpoint anónimo** (login, register, refresh-token) las búsquedas normales de `User` van a devolver null/vacío porque no hay tenant en contexto todavía. Usar la variante `IgnoreQueryFilters()` del repo (ver `GetByEmailAsync` / `GetByIdIgnoringTenantAsync` como referencia) para cualquier lookup pre-autenticación.
3. **Result pattern**: los métodos de aplicación devuelven `Result<T>` / `Error.Validation(...)` / `Error.Conflict(...)`, no excepciones para flujo de negocio.
4. **FastEndpoints**: todo endpoint nuevo requiere JWT por default — usar `AllowAnonymous()` explícito solo cuando de verdad no debe llevar sesión (login, register, refresh-token, webhooks).
5. **RMapper** para mapeo DTO↔entidad (no AutoMapper).
6. **Migrations**: revisar que el nombre de columna generado coincide con `HasColumnName` del snapshot antes de escribir SQL crudo en una migración.
7. **No tocar infraestructura compartida sin avisar**: si una migración habilita extensiones de Postgres o cambia algo a nivel de rol/BD, confirmar con el equipo antes (Neon es una BD dedicada a Comanda, pero mismo criterio aplica).

## Git

- **Commits directos a `main`** — este repo NO usa flujo de feature-branch/PR obligatorio como POS. Se puede commitear directo, pero igual conviene mensajes claros tipo conventional commits (`feat:`, `fix:`, `chore:`).
- Ramas `qa` y `feature/*` existen para trabajo puntual, pero el grueso del desarrollo va directo a `main`.
- **Nunca** agregar `Co-Authored-By: Claude` ni cualquier firma de IA en commits o PRs.
- Verificar `git config user.email` antes de commitear si usas una cuenta de GitHub distinta a la personal — este repo usa el email asociado a `RafaelPadilla19`.

## Deploy

```
dotnet build
gcloud builds submit --tag gcr.io/comanda-501203/comanda-api --project comanda-501203 .
gcloud run deploy comanda-api --image gcr.io/comanda-501203/comanda-api --region=us-central1 --project comanda-501203
```

- **Siempre pedir OK antes de desplegar** a producción (no hay ambiente qa en la nube para este backend todavía).
- Secrets vía Secret Manager (`comanda-db`, JWT keys, etc.), nunca hardcodeados ni en `.env` commiteado.
- Después de cualquier cambio en auth/tenant, probar con un tenant de prueba y **limpiarlo** de la BD al terminar (no dejar basura en prod).

## Documentación

- `docs/auth-api.md` — contrato de los endpoints de auth (login, register, refresh-token, verify-token, login-with-token, revoke-token).
- Si agregás o cambiás un endpoint de auth, actualizá ese doc en el mismo commit.
