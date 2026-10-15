# 🔐 Autenticación — Requisitos y contrato

Documentación de los endpoints de autenticación (requisitos: lista de endpoints, crear
cuenta con los mismos datos que login, login, renovar token, verificar vigencia, y
opcionalmente login con token válido).

## 📑 1. Lista de endpoints

| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| `POST` | `/api/auth/login` | ❌ No requiere | Login con email/password → sesión completa |
| `POST` | `/api/auth/register` | ❌ No requiere | Crea el restaurante + usuario admin → sesión completa (mismo shape que Login) |
| `POST` | `/api/auth/refresh-token` | ❌ No requiere | Canjea un refresh token vigente por una sesión nueva |
| `GET` | `/api/auth/verify-token` | ✅ Bearer Token | Confirma si el access token actual sigue vigente |
| `POST` | `/api/auth/login-with-token` | ✅ Bearer Token | (Opcional) Re-emite sesión completa sin pedir credenciales, usando el JWT vigente |
| `GET` | `/api/auth/me` | ✅ Bearer Token | Ya existente — datos del usuario autenticado + plan/suscripción |
| `POST` | `/api/auth/forgot-password` | ❌ No requiere | Olvidé mi contraseña — envía un OTP de 6 dígitos al correo |
| `POST` | `/api/auth/verify-otp` | ❌ No requiere | Canjea el OTP por un token de un solo uso (15 min) |
| `POST` | `/api/auth/reset-password` | ❌ No requiere | Aplica la nueva contraseña con ese token → sesión completa (mismo shape que Login) |

Todos los errores (login fallido, token inválido, validación, etc.) vuelven con este shape:
```json
{ "codigo": "auth.credenciales", "mensaje": "Correo o contraseña incorrectos." }
```

El objeto de sesión (`LoginResponse`) es **el mismo shape** en Login, Register,
RefreshToken y LoginWithToken — el cliente maneja un único tipo de respuesta sin importar
cómo obtuvo la sesión:
```ts
{
  token: string;         // JWT — se manda en "Authorization: Bearer <token>"
  expiresAt: string;     // ISO 8601 UTC — cuándo vence el token
  refreshToken: string;  // opaco, de un solo uso (ver sección 4)
  user: UserDto;
}
```

---

## 2. `POST /api/auth/login`

**Auth:** ❌ No requiere

**Request body:**
```json
{
  "email": "admin@sabores.sv",
  "password": "Pass123!"
}
```

### Response 200 — credenciales correctas
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-10-07T18:00:00Z",
  "refreshToken": "f3b1c9a2e7d4b8c0a1f6e5d3c2b1a0f9e8d7c6b5a4f3e2d1c0b9a8f7e6d5c4b3",
  "user": {
    "id": "a1b2c3d4-...",
    "name": "Rosa",
    "email": "admin@sabores.sv",
    "role": "Administradora",
    "branchId": null,
    "branchName": "Todas las sucursales",
    "tenantName": "Sabores del Puerto",
    "isActive": true,
    "plan": { "planName": "Premium", "onlinePayments": true, "coupons": true, "loyalty": true, "advancedReports": true, "inventory": false },
    "subscriptionStatus": "Trial",
    "subscriptionEndsAt": null,
    "trialEndsAt": "2027-01-24T00:00:00Z",
    "currency": "USD",
    "currencySymbol": "$",
    "taxRate": 0.13
  }
}
```

### Response 401 — credenciales incorrectas
```json
{ "codigo": "auth.credenciales", "mensaje": "Correo o contraseña incorrectos." }
```

### Response 403 — usuario inactivo
```json
{ "codigo": "auth.inactivo", "mensaje": "Tu usuario está inactivo. Contacta al administrador." }
```

### Response 403 — restaurante suspendido/cancelado
```json
{ "codigo": "auth.suspendido", "mensaje": "La cuenta del restaurante está suspendida. Contacta a soporte." }
```

### Response 400 — email/password con formato inválido
```json
{ "codigo": "...", "mensaje": "El correo no tiene un formato válido." }
```
(Validación de FluentValidation — email vacío o mal formado, password vacío.)

### Response 429 — demasiados intentos
Rate limit de 5 intentos/minuto por IP (igual que el resto del login). Sin body, solo el
status 429.

---

## 3. `POST /api/auth/register`

Crea el tenant (restaurante), el usuario administrador y los datos semilla, y devuelve
sesión completa — **requisito explícito**: registrarse deja al usuario logueado, sin un
segundo viaje a Login.

**Auth:** ❌ No requiere

**Request body:**
```json
{
  "restaurantName": "Sabores del Puerto",
  "adminName": "Rosa",
  "email": "rosa@saboresdelpuerto.sv",
  "password": "Pass123!",
  "country": "SV"
}
```

### Response 200 — registro exitoso
Mismo shape que Login (`LoginResponse`), incluido `refreshToken`.

### Response 409 — email ya registrado
```json
{ "codigo": "auth.email_existe", "mensaje": "Ya existe una cuenta con ese correo." }
```

### Response 400 — validación de campos
```json
{ "codigo": "...", "mensaje": "Selecciona un país válido." }
```
(Nombre del restaurante vacío, contraseña corta, país no soportado, etc. — ver
`RegisterValidator`.)

---

## 4. `POST /api/auth/refresh-token`

**Auth:** ❌ No requiere (el access token ya puede estar vencido — para eso es este endpoint)

**Request body:**
```json
{ "refreshToken": "a1b2c3d4e5f6..." }
```

### Response 200 — refresh token válido
Devuelve una sesión **nueva** (token nuevo y **refresh token nuevo también** — ver
rotación abajo), mismo shape que Login:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-10-08T02:00:00Z",
  "refreshToken": "nuevo-token-aqui...",
  "user": { "...": "..." }
}
```

### Response 401 — refresh token inválido, usado o vencido
```json
{ "codigo": "auth.refresh_invalido", "mensaje": "El refresh token es inválido, ya fue usado o venció. Inicia sesión de nuevo." }
```
Pasa en 4 casos: (a) el token no existe, (b) ya fue usado antes (rotación — ver nota),
(c) venció (por defecto 30 días desde que se emitió), o (d) el usuario dueño ya no está
activo.

**Rotación (importante para el cliente):** cada vez que se usa un refresh token, ese
token queda **revocado** y se emite uno nuevo junto con el access token nuevo. El cliente
SIEMPRE debe guardar el `refreshToken` que viene en la respuesta y descartar el anterior
— si lo reusa, el segundo intento falla con el 401 de arriba. Esto es intencional: si
alguien roba un refresh token viejo de un log o backup, ya no sirve una vez que el dueño
real lo usó.

### Response 400 — falta el campo
```json
{ "codigo": "...", "mensaje": "Falta el refresh token." }
```

### Response 429 — demasiados intentos
Mismo rate limit que Login (comparte la política `"login"`).

---

## 5. `GET /api/auth/verify-token`

Confirma si el access token actual sigue siendo válido, sin tocar la base de datos — si
el JWT llegó hasta el endpoint, el middleware de autenticación ya validó firma y
vigencia antes.

**Auth:** ✅ Bearer Token

```http
GET /api/auth/verify-token
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Response 200 — token vigente
```json
{
  "isValid": true,
  "userId": "a1b2c3d4-...",
  "email": "admin@sabores.sv",
  "tenantId": "f6e5d4c3-...",
  "expiresAt": "2026-10-07T18:00:00Z"
}
```

### Response 401 — token inválido, vencido o ausente
No llega a ejecutar el endpoint — lo corta el middleware de autenticación de ASP.NET
Core antes. El body viene vacío (sin JSON). El cliente debe tratar **cualquier 401** de
este endpoint como "necesito renovar con `/auth/refresh-token` o re-loguearme" — no
intentar parsear un body que puede no venir.

---

## 6. `POST /api/auth/login-with-token` (opcional)

Si el cliente todavía tiene un access token vigente, re-emite una sesión completa
(access + refresh token **nuevos**) sin pedirle credenciales al usuario — útil para
extender la sesión de alguien activo sin forzar un refresh explícito ni un login
completo.

**Auth:** ✅ Bearer Token

```http
POST /api/auth/login-with-token
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Response 200
Mismo shape que Login/Register/RefreshToken.

### Response 401 — token inválido/vencido
Igual que VerifyToken: lo corta el middleware antes de llegar al endpoint.

**Nota:** este endpoint **no revoca** el refresh token que el cliente ya tenía guardado
de antes (si tenía uno) — simplemente emite uno nuevo adicional. Si el cliente quiere
mantener una sola sesión activa a la vez, debe descartar el refresh token viejo por su
cuenta.

---

## 7. Olvidé mi contraseña — OTP en 3 pasos

Flujo: `forgot-password` (pide el OTP) → `verify-otp` (canjea el OTP por un token) →
`reset-password` (aplica la nueva contraseña con ese token). El OTP nunca se reutiliza
como credencial del paso 3 — una vez verificado, solo el token opaco sirve.

### 7.1 `POST /api/auth/forgot-password`

```json
{ "email": "admin@sabores.sv" }
```

**Response 200 — siempre el mismo mensaje**, exista o no el correo (no revela qué cuentas
están registradas):
```json
{ "mensaje": "Si el correo está registrado, recibirás un código de verificación." }
```

Si el correo pertenece a un usuario activo, se genera un OTP numérico de 6 dígitos
(vigente 10 minutos) y se envía por correo (plantilla HTML, remitente `Smtp:FromName`).
Un fallo de envío no se refleja en la respuesta (se registra y se ignora) — el mensaje es
siempre el mismo. Rate limit: política `password-reset` (5 req/min por IP).

### 7.2 `POST /api/auth/verify-otp`

```json
{ "email": "admin@sabores.sv", "code": "123456" }
```

**Response 200:**
```json
{ "token": "a1XaL020l1a3t1ktU_KLotbRx8HG6pngspE9hRWx6co" }
```

**Response 401** (`auth.otp_invalido`) si el código es incorrecto, ya expiró, ya fue
usado, o se superaron 5 intentos fallidos. El token devuelto es opaco (256 bits,
Base64Url), vigente 15 minutos, y de un solo uso.

### 7.3 `POST /api/auth/reset-password`

```json
{ "token": "a1XaL020l1a3t1ktU_KLotbRx8HG6pngspE9hRWx6co", "newPassword": "NuevaPass123" }
```

**Response 200 — mismo shape que `/api/auth/login`** (sesión completa: JWT + refresh
token + `UserDto`). **Response 401** (`auth.reset_token_invalido`) si el token es
inválido, ya se usó, o expiró.

### 7.4 Tabla `PasswordResetRequests` (nueva)

Entidad `Comanda.Domain.Entities.PasswordResetRequest`, no es `ITenantScoped` (se busca
por email antes de conocer el tenant, igual que Login/Register), migración
`AddPasswordResetRequests`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid (PK) | |
| `Email` | varchar(200) | normalizado a minúsculas |
| `OtpHash` | varchar(100) | SHA-256 del OTP — nunca se guarda en claro |
| `OtpExpiresAt` | timestamptz | 10 minutos desde la creación |
| `Attempts` | int | máximo 5 intentos fallidos de verificación |
| `ResetToken` | varchar(200), único, nullable | se llena al verificar el OTP correctamente |
| `ResetTokenExpiresAt` | timestamptz, nullable | 15 minutos desde la verificación |
| `Used` | bool | se marca tras un reset exitoso |
| `CreatedAt` | timestamptz | heredado de `Entity` |

**Envío de correo:** `Comanda.Infrastructure.Email.SmtpEmailSender` (MailKit), configurado
por variables de entorno (`Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`,
`Smtp__FromName`, `Smtp__UseSsl`) — nunca hardcodeadas ni en `appsettings` versionado.

---

## 8. Tabla `RefreshTokens` (nueva)

Entidad nueva (`Comanda.Domain.Entities.RefreshToken`), tabla `RefreshTokens` en
`ComandaDbContext` (no es `ITenantScoped` — el `UserId` ya identifica el tenant vía el
usuario), migración `AddRefreshTokens`:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid (PK) | |
| `UserId` | uuid (FK → Users) | `ON DELETE CASCADE` |
| `Token` | varchar(200), único | valor aleatorio de 256 bits, Base64Url |
| `ExpiresAt` | timestamptz | |
| `RevokedAt` | timestamptz, nullable | se llena al rotar |
| `ReplacedByToken` | varchar(200), nullable | trazabilidad de la rotación |
| `CreatedAt` | timestamptz | heredado de `Entity` |

**Duración del access token:** `Jwt:ExpiryMinutes` (default 480 = 8h).
**Duración del refresh token:** `Jwt:RefreshTokenExpirationDays` (default 30 días).

La migración se aplica igual que las demás de este repo: manualmente contra Neon
(`dotnet ef database update`), no hay CI/CD automático en Comanda.Backend.

---

## 9. Códigos de respuesta — resumen

| Código | Cuándo |
|--------|-------------|
| 200 | Éxito |
| 400 | Validación de campos (FluentValidation) |
| 401 | Credenciales incorrectas, refresh token inválido/usado/vencido, OTP/token de reset inválido, o access token inválido/vencido en los endpoints `[Authorize]` |
| 403 | Usuario inactivo o restaurante suspendido/cancelado |
| 409 | Email ya registrado (solo en `/auth/register`) |
| 429 | Rate limit excedido (`/auth/login`, `/auth/refresh-token` y el flujo de `/auth/forgot-password`\|`verify-otp`\|`reset-password`) |
