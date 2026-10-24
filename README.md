# payment-service — migraciones Liquibase

Schemas propios: `promotion` y `payment` (12 tablas). No depende de ningún otro servicio.
Los changelogs son SQL puro: los ejecuta Docker (Liquibase), no tu código C#.

## Levantar todo con Docker (no necesitas instalar Liquibase)

```bash
docker compose up --abort-on-container-exit migrate
```

Esto arranca SQL Server, crea la base `lavado_vehicular` y aplica los changelogs.
Cuando termine, el contenedor `pay-sqlserver` queda escuchando en `localhost:1434`.

## Comprobar que se crearon las 12 tablas

```bash
MSYS_NO_PATHCONV=1 docker exec pay-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$DB_PASSWORD" -d lavado_vehicular \
  -Q "SELECT s.name AS esquema, COUNT(*) AS tablas FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name IN ('promotion','payment') GROUP BY s.name"
```

(`$DB_PASSWORD` es la misma contraseña que pusiste en `lavarapido-infra/.env`. Nunca la escribas en claro en un commit.)

Resultado esperado: `payment` = 7 y `promotion` = 5 (más las 2 tablas de control de Liquibase en `dbo`).

## Volver a empezar desde cero

```bash
docker compose down -v
docker compose up --abort-on-container-exit migrate
```

## Con el CLI de Liquibase instalado (opcional)

```bash
export LIQUIBASE_COMMAND_PASSWORD="$DB_PASSWORD"
liquibase update
```

## Tablas de control

Usa `DATABASECHANGELOG_PAYMENT` y `DATABASECHANGELOGLOCK_PAYMENT`, para no chocar con
otros servicios si comparten la misma base en un ambiente común.

## Verificación importante

`payment_status` debe quedar con `APPROVED` en el ID 3 (el índice único filtrado lo usa).
La migración se detiene sola si no es así.

---

# Solución C# / ASP.NET Core

## Arquitectura hexagonal

Las dependencias apuntan siempre hacia el dominio. El dominio no conoce a nadie; la aplicación
define **puertos** (interfaces) y la infraestructura y la API los implementan como **adaptadores**.

```
        Api (adaptadores de entrada: HTTP)          Infrastructure (adaptadores de salida)
                     │                                  │  EF Core · booking-service · RabbitMQ
                     ▼                                  ▼
              Ports/In ──► Application ◄── Ports/Out
                               │
                               ▼
                             Domain   (sin dependencias)
```

```
src/
├── PaymentService.Domain/                 ← reglas de negocio puras, sin EF, HTTP, tablas ni reloj del sistema
│   ├── Common/          Entity, AggregateRoot (+ IAggregateRoot), DomainEvent, DomainException,
│   │                    DomainErrorCodes, Guard, ImageSource
│   ├── Payments/        Payment (aggregate: máquina de estados), PaymentReceipt, PaymentStatus,
│   │                    PaymentPolicy (reserva pagable, monto a pagar), eventos
│   ├── PaymentAccounts/ PaymentAccount, PaymentMethodType
│   ├── Promotions/      Promotion (aggregate), PromotionRedemption, DiscountType, PromotionRedeemed,
│   │   └── Discounts/   IDiscountStrategy + Percentage/FixedAmount + DiscountStrategyFactory
│   └── Loyalty/         LoyaltyTransaction (ledger), LoyaltyBalance, LoyaltyMovementType
├── PaymentService.Application/            ← casos de uso; solo conoce al dominio
│   ├── Ports/In/        IPaymentCommandUseCases, IPaymentQueryUseCases, IPaymentAccountUseCases,
│   │                    IPromotionAdminUseCases, ICustomerPromotionUseCases, ILoyaltyUseCases
│   ├── Ports/Out/       Persistence (IUnitOfWork + repositorios), Integration (IBookingDirectory,
│   │                    IIntegrationEventPublisher)            (los implementa Infrastructure)
│   ├── Payments/        PaymentCommandService, PaymentQueryService, BookingPaymentGuard,
│   │                    AmountDueCalculator, PaymentDtoAssembler, PaymentIntegrationEvents
│   ├── PaymentAccounts/ PaymentAccountService, PaymentAccountReader
│   ├── Promotions/      PromotionAdminService (admin), CustomerPromotionService (cliente: catálogo y canje),
│   │                    PromotionMapper, PromotionIntegrationEvents
│   ├── Loyalty/         LoyaltyService (saldo), LoyaltyRewardService (acreditar / revertir puntos)
│   ├── Common/          Caller, ErrorCodes, excepciones (InvalidRequest / NotFound / Conflict /
│   │                    ServiceUnavailable), IntegrationEventPublishing
│   └── DependencyInjection.cs   AddApplication()
├── PaymentService.Infrastructure/         ← adaptadores de salida (sin dependencia de ASP.NET)
│   ├── Persistence/     PaymentDbContext, EfUnitOfWork, CatalogIds, PaymentStatusIds,
│   │                    Configurations/ (mapeo a tablas + auditoría/concurrencia), Repositories/
│   ├── Booking/         BookingServiceDirectory (REST a booking-service, capa anticorrupción),
│   │                    AccessTokenForwardingHandler + IAccessTokenProvider (propaga la identidad)
│   ├── Messaging/       RabbitMqIntegrationEventPublisher, NullIntegrationEventPublisher
│   └── DependencyInjection.cs   AddInfrastructure()
└── PaymentService.Api/                    ← adaptadores de entrada + composition root
    ├── Controllers/     Payments, AdminPayments, AdminPaymentAccounts, Promotions, Loyalty
    ├── Contracts/       cuerpos JSON de los requests (las respuestas son los *Dto de Application)
    ├── Errors/          GlobalExceptionHandler (tipo de excepción → 400/404/409/503)
    ├── Http/            HttpContext → Caller, HttpContextAccessTokenProvider
    ├── Configuration/   EnvFile, JWT, CORS, cadena de conexión
    └── Program.cs       registra además TimeProvider e IAccessTokenProvider
```

### Convenciones (para que no aparezcan incoherencias)

- **Contratos**: Application define la entrada de cada caso de uso (`*Command`) y su salida (`*Dto`);
  los `*Dto` se serializan tal cual, así que sus nombres de campo son contrato con la web y la app.
  La Api solo define cómo llegan los datos por HTTP (`*Request`) y los traduce a `*Command`.
  Los modelos de los puertos de salida (por ejemplo `BookingInfo`) nunca salen en una respuesta.
- **Errores**: las reglas de un aggregate lanzan `DomainException` con un código de `DomainErrorCodes`;
  los casos de uso lanzan una subclase de `ApplicationLayerException` con un código de `ErrorCodes`.
- **Tiempo**: el dominio nunca lee el reloj; los casos de uso le pasan el "ahora" de `TimeProvider`.
- **Eventos**: todo hecho que se anuncia nace como evento de dominio en su aggregate; el caso de uso
  confirma, traduce con `*IntegrationEvents` y publica con `PublishAndClearAsync`.
- **Identidad**: los casos de uso solo reciben `Caller(UserId)`; el token para booking-service lo
  agrega `AccessTokenForwardingHandler`.
- **Concurrencia**: `row_version` es token optimista en todas las tablas y un aggregate con eventos
  pendientes cuenta como modificado; el ledger tiene una posición única por cliente (`sequence_no`).
  Un choque con un índice único que protege una regla responde 409 con el código de esa regla
  (`PAYMENT_ALREADY_REPORTED`, `TRANSACTION_REFERENCE_REUSED`, `PAYMENT_ALREADY_APPROVED`,
  `PROMOTION_ONE_PER_BOOKING`); cualquier otro choque, 409 `DATA_CONFLICT`.
- **Validación**: un cuerpo sin un campo obligatorio (`bookingId`, `paymentAccountId`) o con JSON mal
  formado responde 400 con `code` = `VALIDATION_ERROR`, igual que los servicios Java.

### Patrones aplicados

| Patrón | Dónde | Por qué |
|--------|-------|---------|
| Aggregate / Domain Events | `Payment`, `Promotion` | protegen sus invariantes; los eventos se publican después de guardar |
| Factory Method | `Payment.ReportWithReceipt`, `Payment.RegisterInPerson`, `Promotion.Create`, `LoyaltyTransaction.Earn` | los dos caminos reales por los que entra un pago quedan explícitos |
| Strategy + Factory | `Promotions/Discounts` | cada tipo de descuento (PERCENT, FIXED) calcula a su manera, sin `switch` en `Promotion` |
| Repository | `Ports/Out/Persistence` | los casos de uso no saben que hay EF Core |
| Unit of Work | `IUnitOfWork` → `EfUnitOfWork` | aprobar un pago y acreditar sus puntos (o reembolsarlo y revertirlos) se guardan en una sola transacción |
| Concurrencia optimista | `AuditColumns` (`row_version`), `LoyaltyBalance.Sequence` | dos requests simultáneos no se saltan los límites de canje ni dejan un saldo de puntos equivocado |
| Policy (servicio de dominio) | `PaymentPolicy` | reglas que cruzan aggregates (reserva pagable, monto a pagar) siguen en el dominio |
| Propagación de identidad | `AccessTokenForwardingHandler` | los casos de uso no manejan tokens |
| Null Object | `NullIntegrationEventPublisher` | sin RabbitMQ, los casos de uso no preguntan si hay bus |
| Parameter Object | `PromotionDefinition`, `RedemptionRequest` | evita listas de 13 parámetros y deja la regla de canje en el dominio |
| Anti-Corruption Layer | `BookingServiceDirectory` | traduce el JSON de booking-service a `BookingInfo` |
| CQRS ligero | `PaymentCommandService` / `PaymentQueryService` | las consultas no cargan la Unit of Work ni el bus |
| Dependency Injection | `AddApplication()`, `AddInfrastructure()` | `Program.cs` es el único lugar que junta las capas |

### Tablas (`06-data/models.md`) y cómo se cubren

| Tabla | Modelo | Reglas que se hacen cumplir |
|-------|--------|-----------------------------|
| `payment.payment` | `Payment` | monto > 0; estados PENDING → IN_REVIEW → APPROVED/REJECTED → REFUNDED; un solo APPROVED por reserva (validación + índice filtrado → 409); un solo pago abierto (PENDING o IN_REVIEW) por reserva (validación + índice filtrado de la migración 023 → 409 `PAYMENT_ALREADY_REPORTED`); al aprobar, el monto debe seguir siendo lo que se debe hoy (409 `AMOUNT_CHANGED`) |
| `payment.payment_receipt` | `PaymentReceipt` | `reviewed_at`/`reviewed_by` siempre juntos (se llenan al aprobar/rechazar); antifraude: la misma `transaction_reference` no puede pagar dos reservas (validación) ni respaldar dos pagos vivos (vista indexada `v_live_transaction_reference`, migración 023) → 409 `TRANSACTION_REFERENCE_REUSED` |
| `payment.payment_account` / `payment_method_type` | `PaymentAccount`, `PaymentMethodType` | el cliente solo reporta a medios con `requires_receipt`; el efectivo lo registra el admin, también con la cuenta de efectivo desactivada (desactivarla solo la oculta a los clientes) |
| `payment.payment_status` | `PaymentStatus` (IDs fijos en `PaymentStatusIds`, Infrastructure) | IDs verificados por las migraciones 015 y 019 |
| `payment.loyalty_transaction` / `loyalty_movement_type` | `LoyaltyTransaction`, `LoyaltyMovementType` (ID resuelto por código al arrancar, `CatalogIds`) | ledger append-only; `balance_after` nunca negativo; `sequence_no` único por cliente (migración 020); acreditación idempotente por puntos netos de la reserva; el reembolso los revierte (`REVERSED`) |
| `promotion.promotion` / `discount_type` | `Promotion`, `DiscountType` (ID resuelto por código al arrancar: SQL Server puede saltar IDENTITY) | vigencia, puntos requeridos, `min_purchase_amount`, `max_discount_amount`, `max_redemptions`, `max_redemptions_per_customer` |
| `promotion.booking_promotion` | `PromotionRedemption` | una sola promoción por reserva (índice de la migración 021); `applied_amount > 0` es lo que descontó el día del canje; un cupón porcentual guarda su porcentaje y su tope (migración 022) y se recalcula con el total de hoy de la reserva, sin dejarla nunca en 0 |
| `promotion.promotion_customer`, `promotion_service`, columnas `is_public` y `min_completed_booking` | — | **no modelados todavía**: evaluarlos necesita datos de customer-service y booking-service que hoy no se exponen; las filas conservan sus valores por defecto |

### Eventos que publica (carwash.events, con `MESSAGING_ENABLED=true`)

| Evento | Routing key | Cuándo |
|--------|-------------|--------|
| `PaymentConfirmed` | `payment.confirmed` | pago aprobado (también el registrado en persona) |
| `PaymentRejected` | `payment.rejected` | pago rechazado (incluye `reason`) |
| `PaymentRefunded` | `payment.refunded` | pago reembolsado |
| `PromotionRedeemed` | `payment.promotion_redeemed` | cupón canjeado |
| `LoyaltyPointsEarned` | `payment.loyalty_points_earned` | justo después de `payment.confirmed`, si ese pago acreditó puntos; trae el saldo nuevo y los cupones que esos puntos desbloquearon (`unlockedPromotions`) para avisarle al cliente |

En las promociones, `price` y `durationMinutes` son opcionales: la web y la app ya no los piden (el
cupón es un descuento sobre la reserva, ADR-015 sección 5). Si llegan, deben ser mayores que cero.

### Endpoints nuevos o que cambiaron (23-10-2026)

La tabla completa de endpoints está en [docs/GUIA-PAYMENT-SERVICE.md](docs/GUIA-PAYMENT-SERVICE.md#73-endpoints).
Todas las rutas empiezan con `/api/v1`.

| Método y ruta | Rol | Qué cambió |
|---|---|---|
| `GET /loyalty/redemptions/{bookingId}` | CLIENT | Nuevo (BIZ-02): `{bookingId, bookingTotal, appliedDiscounts, amountDue, redemption}`; `redemption` es `{promotionId, promotionCode, promotionName, discountAmount}` o `null`. Reserva ajena → 404 `BOOKING_NOT_FOUND` |
| `DELETE /loyalty/redemptions/{bookingId}/{promotionId}` | CLIENT | Quitar el cupón (E-08); una sola promoción por reserva (E-09) |
| `GET /admin/payments?status=&page=&size=` | ADMIN | Con `page` o `size` (E-10) responde `{items, page, size, totalElements, totalPages}` (page desde 0, size 1-100, 20 por defecto) sin la imagen del comprobante; sin ellos, el arreglo de siempre (hasta 200, con imagen) |
| `GET /admin/payments/{id}` | ADMIN | Es la forma de pedir la imagen del comprobante de un pago de la página |
| `POST /admin/payments/{id}/approve` | ADMIN | 409 `AMOUNT_CHANGED` si el monto del pago ya no es lo que se debe hoy de la reserva (BIZ-01) |
| `POST /payments`, `POST /admin/payments`, `POST /loyalty/redeem` | — | `bookingId` y `paymentAccountId` obligatorios: 400 `VALIDATION_ERROR` si faltan (BIZ-13) |

Campos nuevos en las respuestas (los anteriores no cambian):

- `PaymentDto.hasReceiptImage`: si el comprobante tiene imagen (E-10).
- `PaymentDto.bookingCancelled`: la reserva está cancelada hoy en booking-service; con un pago
  `APPROVED` el admin debe reembolsarlo, con uno `IN_REVIEW`, rechazarlo (BIZ-07).
- `PromotionForCustomerDto.discountType` (`PERCENT` o `FIXED`) y `discountValue` (D-01), los mismos
  que ya tenía `PromotionDto` del admin.

## Levantarlo con el resto del backend (recomendado)

El servicio ya está integrado en `docker-compose.yml` de `lavarapido-infra` (perfil `app`), en el
puerto **3005**, enrutado por el api-gateway en `/api/v1/payments/**` y `/api/v1/payment-accounts`.
Lee las variables de `../lavarapido-infra/.env` igual que los servicios Java (`EnvFile.cs`).

```bash
cd ../lavarapido-infra
docker compose --profile app up -d --build
```

No necesitas repetir los pasos de Liquibase de este README: `payment-migrate` ya corre los
changelogs contra la base compartida `LavaRapido` antes de que arranque este servicio.

Dependencias (las flechas indican quién conoce a quién):

```
Api ──► Application ──► Domain
 └────► Infrastructure ──► Application
```

## 1. Versión de .NET

El proyecto usa **net8.0**, que funciona con Visual Studio 2022 (17.8 o superior).
Para ver tu versión de Visual Studio: menú Ayuda > Acerca de Microsoft Visual Studio.
No cambies la versión a net10.0 en Visual Studio 2022: esa versión pide Visual Studio 2026.

## Abrir en Visual Studio 2022

1. Menú Archivo > Abrir > Proyecto o solución > selecciona `PaymentService.sln`.
2. Espera a que termine de restaurar paquetes NuGet (barra inferior). Necesita internet la primera vez.
3. Clic derecho en `PaymentService.Api` > Establecer como proyecto de inicio.
4. Ctrl+Shift+B para compilar. Menú Prueba > Explorador de pruebas para correr los tests.
5. F5 para levantar la API (escucha en http://localhost:3005, o en el puerto de `PAYMENT_PORT`).

Requiere la carga de trabajo "ASP.NET y desarrollo web" del instalador de Visual Studio.

## 2. Compilar y probar

```bash
dotnet build
dotnet test
```

Esperado: `Build succeeded` con 0 errores, y 225 pruebas pasadas al 24-10-2026 (con los arreglos del
23-10 fusionados): 134 del dominio, 52 de los casos de uso (prueban Application con dobles en
memoria de sus puertos de salida), 34 de la capa HTTP (`PaymentService.Api.UnitTests`: 27 de
autorización por rol (F-04) y 7 de campos obligatorios y formato de los errores de validación) y 5
de infraestructura que no necesitan base (qué 409 corresponde a cada índice único). Además hay 11
de integración, que salen como omitidas si no se configura su base (ver abajo).

### Pruebas de integración (contra SQL Server real, opcionales)

Prueban lo que los tests unitarios no pueden: consultas de EF, concurrencia (`row_version`,
`sequence_no`) y que la base acepte lo que el código escribe. Usan la base **aislada** de este repo
(puerto 1434), nunca la compartida del equipo (1433): insertan filas de prueba.

La contraseña de esa base sale de `MSSQL_SA_PASSWORD` en el `.env` de este repo (copia
`.env.example`; el `.env` no se sube). No hay contraseña por defecto en el repositorio (E-05).

```bash
# 1. Levantar la base aislada y aplicar todas las migraciones
docker compose up --abort-on-container-exit migrate

# 2. Correr las pruebas apuntando a esa base (en Git Bash)
set -a; . ./.env; set +a
PAYMENT_INTEGRATION_DB="Server=localhost,1434;Database=lavado_vehicular;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True" \
  dotnet test tests/PaymentService.Infrastructure.IntegrationTests
```

En PowerShell el paso 2 es:

```powershell
$env:MSSQL_SA_PASSWORD = (Get-Content .env | Where-Object { $_ -like 'MSSQL_SA_PASSWORD=*' }) -replace '^MSSQL_SA_PASSWORD=', ''
$env:PAYMENT_INTEGRATION_DB = "Server=localhost,1434;Database=lavado_vehicular;User Id=sa;Password=$env:MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet test tests/PaymentService.Infrastructure.IntegrationTests
```

Sin la variable, estas 11 pruebas aparecen como **omitidas** (no fallan). Para borrar la base de
prueba y empezar de cero: `docker compose down -v`.

## 3. Levantar la API y ver que responde

```bash
dotnet run --project src/PaymentService.Api
```

En otra terminal:

```bash
curl http://localhost:3005/health
```

Esperado: `{"service":"payment-service","status":"ok"}`

## Proyecto LavaRápido

Este repositorio es parte de LavaRápido (Vehicle Washing), proyecto formativo del Tecnólogo en
Análisis y Desarrollo de Software (ADSO), ficha 3145556, SENA Regional Huila.

| Recurso | Enlace |
|---|---|
| Documentación y manuales (ES/EN, MD · PDF · Word) | [lavarapido-docs](https://github.com/BrayanCABRERA123/lavarapido-docs#readme) |
| Manual técnico | [09_MANUALES/ES/MD/manual-tecnico.md](https://github.com/BrayanCABRERA123/lavarapido-docs/blob/main/09_MANUALES/ES/MD/manual-tecnico.md) |
| Manual de instalación | [09_MANUALES/ES/MD/manual-de-instalacion.md](https://github.com/BrayanCABRERA123/lavarapido-docs/blob/main/09_MANUALES/ES/MD/manual-de-instalacion.md) |
| Levantar todo el sistema | [lavarapido-infra](https://github.com/BrayanCABRERA123/lavarapido-infra) (`docker compose --profile app up -d`) |
| Aplicación desplegada | https://lavarapidovehicular.northcentralus.cloudapp.azure.com |

**Ramas y commits:** se trabaja en `develop` con ramas `feature/`, `fix/` y `docs/` que se
fusionan con `git merge --no-ff`; los commits siguen Conventional Commits
(`feat(scope): ...`, `fix(scope): ...`). Detalle en la
[guía de git del equipo](https://github.com/BrayanCABRERA123/lavarapido-docs#guía-de-git-del-equipo).

**Equipo:** Brayan Estiven Patiño Cabrera, Juan Camilo Guilombo Osorio y Merari Urbano Quintero.
