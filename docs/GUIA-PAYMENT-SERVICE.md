# Guía completa de payment-service

Esta guía explica **qué hace** payment-service, **cómo está construido** y **cómo fluye cada
operación** de punta a punta. Está pensada para leerse en orden la primera vez y luego usarse
como referencia. El `README.md` del repo cubre cómo levantarlo; aquí está el *por qué* y el
*cómo funciona*.

## Contenido

1. [Qué es y de qué se encarga](#1-qué-es-y-de-qué-se-encarga)
2. [Dónde vive dentro del sistema](#2-dónde-vive-dentro-del-sistema)
3. [Arquitectura hexagonal](#3-arquitectura-hexagonal)
4. [Capa de dominio](#4-capa-de-dominio)
5. [Capa de aplicación](#5-capa-de-aplicación)
6. [Capa de infraestructura](#6-capa-de-infraestructura)
7. [Capa de API](#7-capa-de-api)
8. [Flujos completos paso a paso](#8-flujos-completos-paso-a-paso)
9. [Base de datos](#9-base-de-datos)
10. [Eventos que publica](#10-eventos-que-publica)
11. [Consistencia y concurrencia](#11-consistencia-y-concurrencia)
12. [Errores](#12-errores)
13. [Configuración](#13-configuración)
14. [Pruebas](#14-pruebas)
15. [Cómo lo consumen la web y el móvil](#15-cómo-lo-consumen-la-web-y-el-móvil)
16. [Limitaciones conocidas y pendientes](#16-limitaciones-conocidas-y-pendientes)
17. [Glosario](#17-glosario)

---

## 1. Qué es y de qué se encarga

payment-service es el microservicio (ASP.NET Core 8, C#) dueño de **todo lo que tiene que ver con
dinero y beneficios** en LavaRápido. Tiene cuatro responsabilidades:

| Área | Qué hace |
|------|----------|
| **Pagos** | El cliente reporta que pagó (con foto del comprobante); el admin lo aprueba, rechaza o reembolsa. El admin también registra pagos recibidos en caja. |
| **Cuentas de pago** | Las cuentas del lavadero (Nequi, Daviplata, transferencia, efectivo) con su QR, que el cliente ve al pagar. |
| **Promociones / cupones** | El admin crea promociones; el cliente canjea un cupón sobre una reserva y obtiene un descuento real. |
| **Fidelización (puntos)** | Ledger de puntos: se ganan cuando una reserva queda pagada y se revierten si el pago se reembolsa. Los puntos **desbloquean** promociones (no se gastan). |

Lo que **no** hace: no crea ni modifica reservas (eso es booking-service), no envía correos ni
notificaciones (eso es notification-service, que escucha los eventos de este servicio) y no
maneja usuarios ni login (security-service).

### Principios que guían todo el servicio

- **El monto lo decide el servidor, nunca el navegador.** Lo que se cobra sale del total de la
  reserva (booking-service) menos los cupones canjeados.
- **No se cobra dos veces.** Una reserva tiene como máximo un pago aprobado, garantizado por la
  aplicación y por la base de datos.
- **Puntos y pago van juntos.** Aprobar un pago y acreditar sus puntos se guardan en la misma
  transacción; reembolsar y revertir los puntos, también.
- **Las reglas de negocio viven en el dominio**, no en los controllers ni en SQL.

---

## 2. Dónde vive dentro del sistema

```
   Web (Angular)        App móvil (React Native)
          │                      │
          └──────────┬───────────┘
                     ▼
             api-gateway :8080          (enruta /api/v1/payments/**, /admin/payments/**,
                     │                    /payment-accounts, /admin/promotions/**, /loyalty/** ...)
                     ▼
          ┌─────────────────────┐   HTTP + token del usuario   ┌──────────────────┐
          │  payment-service    │ ───────────────────────────► │ booking-service  │
          │      :3005          │   (consulta reservas)        │      :3003       │
          └─────────────────────┘                              └──────────────────┘
             │              │
             │ EF Core      │ RabbitMQ (exchange carwash.events)
             ▼              ▼
     SQL Server        notification-service  (avisos in-app y correos)
  esquemas payment
  y promotion
```

- **Entrada:** siempre por el api-gateway, con un JWT emitido por security-service.
- **Booking-service:** es la fuente de verdad de las reservas. payment-service le pregunta el
  total, el estado, el dueño y los puntos de cada reserva. Le reenvía el token del usuario, así
  booking aplica sus propias reglas (un cliente solo ve sus reservas).
- **Base de datos:** SQL Server compartido (`LavaRapido`), pero este servicio solo toca sus dos
  esquemas: `payment` y `promotion`. Las referencias a reservas o usuarios son IDs sin llave
  foránea (viven en otros servicios).
- **RabbitMQ:** publica eventos cuando un pago se aprueba, rechaza o reembolsa, y cuando se canjea
  un cupón. notification-service los convierte en avisos para el cliente.

---

## 3. Arquitectura hexagonal

El servicio sigue **arquitectura hexagonal** (también llamada *puertos y adaptadores*) con
**DDD táctico** (aggregates, value objects, eventos de dominio).

### La regla de oro: las dependencias apuntan hacia adentro

```
        ┌──────────────────────────────────────────────────────────┐
        │  Api  (adaptadores de ENTRADA: HTTP, JWT, errores)        │
        │   ┌──────────────────────────────────────────────────┐   │
        │   │  Application  (casos de uso + PUERTOS)           │   │
        │   │   ┌──────────────────────────────────────────┐   │   │
        │   │   │  Domain  (reglas de negocio puras)       │   │   │
        │   │   └──────────────────────────────────────────┘   │   │
        │   └──────────────────────────────────────────────────┘   │
        │  Infrastructure  (adaptadores de SALIDA: EF Core,         │
        │                   booking-service, RabbitMQ)              │
        └──────────────────────────────────────────────────────────┘

   Api ──► Application ──► Domain
    └────► Infrastructure ──► Application
```

- **Domain** no conoce a nadie: no sabe que existe una base de datos, HTTP ni el reloj del
  sistema. Solo C# puro.
- **Application** conoce al dominio y define **puertos**: interfaces que dicen *qué necesita*
  (guardar un pago, consultar una reserva, publicar un evento) sin decir *cómo*.
  - **Puertos de entrada** (`Ports/In`): lo que el servicio ofrece (casos de uso). Los usan los
    controllers.
  - **Puertos de salida** (`Ports/Out`): lo que el servicio necesita de afuera. Los implementa
    Infrastructure.
- **Infrastructure** implementa los puertos de salida con tecnología concreta (EF Core, HttpClient,
  RabbitMQ).
- **Api** implementa los puertos de entrada vía HTTP y es el **composition root**: el único lugar
  (`Program.cs`) que conecta todas las capas.

**¿Para qué sirve?** Cambiar SQL Server por otra base, o RabbitMQ por otro bus, solo toca
Infrastructure. Las reglas de negocio se prueban sin base de datos ni servidor (dobles en memoria).

### Estructura de carpetas

```
src/
├── PaymentService.Domain/            Reglas de negocio puras
│   ├── Common/                       AggregateRoot, Entity, Guard, DomainException, ImageSource...
│   ├── Payments/                     Payment, PaymentReceipt, PaymentStatus, PaymentPolicy, eventos
│   ├── PaymentAccounts/              PaymentAccount, PaymentMethodType
│   ├── Promotions/                   Promotion, PromotionRedemption, RedemptionRequest, eventos
│   │   └── Discounts/                IDiscountStrategy + Percentage / FixedAmount + Factory
│   └── Loyalty/                      LoyaltyTransaction, LoyaltyBalance, LoyaltyMovementType
├── PaymentService.Application/       Casos de uso
│   ├── Ports/In/                     Interfaces de los casos de uso
│   ├── Ports/Out/                    Persistence (repositorios, IUnitOfWork) e Integration
│   ├── Payments/                     Comandos, consultas, guardas, calculadora de monto, DTOs
│   ├── PaymentAccounts/              Gestión de cuentas
│   ├── Promotions/                   Admin de promociones y canje del cliente
│   ├── Loyalty/                      Saldo y acreditación/reversión de puntos
│   └── Common/                       Caller, ErrorCodes, excepciones, publicación de eventos
├── PaymentService.Infrastructure/    Adaptadores de salida
│   ├── Persistence/                  DbContext, UnitOfWork, configuraciones EF, repositorios
│   ├── Booking/                      Cliente HTTP de booking-service (anticorrupción)
│   └── Messaging/                    Publicador RabbitMQ y publicador nulo
└── PaymentService.Api/               Adaptadores de entrada + arranque
    ├── Controllers/                  Endpoints HTTP
    ├── Contracts/                    Cuerpos JSON de los requests
    ├── Errors/                       Excepción → respuesta HTTP
    ├── Http/                         Token y usuario del request
    ├── Configuration/                JWT, CORS, cadena de conexión, .env
    └── Program.cs                    Composition root
```

---

## 4. Capa de dominio

Aquí están **todas las reglas del negocio**. Ninguna clase de esta capa sabe de base de datos ni
HTTP; por eso se puede probar con pruebas unitarias simples (97 pruebas).

### 4.1 Bloques comunes (`Domain/Common`)

| Clase | Para qué sirve |
|-------|----------------|
| `Entity<TId>` | Base de todo lo que tiene identidad (Id). |
| `AggregateRoot<TId>` | Raíz de un aggregate: la única entidad que el resto del sistema puede modificar. Acumula **eventos de dominio** pendientes de publicar. |
| `Guard` | Validaciones de una línea (`Guard.Against`, `Guard.Required`, `Guard.PositiveId`). Si fallan, lanzan `DomainException` con un código. |
| `DomainException` + `DomainErrorCodes` | Error de regla de negocio, con un código estable (ej. `INVALID_PAYMENT_AMOUNT`). La API lo convierte en HTTP 400. |
| `ImageSource` | Regla única de "esto es una imagen válida": solo `data:image/png`, `jpeg`, `webp` o `gif` en base64. Rechaza SVG (puede llevar scripts) y URLs externas. |

**Regla del tiempo:** el dominio nunca lee el reloj. Los métodos reciben `nowUtc` desde el caso
de uso. Así las pruebas controlan las fechas y los resultados son deterministas.

### 4.2 Pagos (`Domain/Payments`)

#### `Payment` — el aggregate principal

Representa **un intento de pago** de una reserva. Una reserva puede tener varios intentos (por
ejemplo uno rechazado y luego otro aprobado), pero como máximo uno aprobado.

Datos: `BookingId`, `PaymentAccountId`, `Amount` (lo que se espera cobrar), `Status`,
`ProcessedAtUtc`, `ApprovedBy`, `RejectionReason` y la lista de `Receipts` (comprobantes).

**Máquina de estados:**

```
                 adjuntar comprobante
   PENDING ──────────────────────────► IN_REVIEW ───Approve──► APPROVED ───Refund──► REFUNDED
                (SubmitForReview)          │                                       (final)
                                           └──────Reject──────► REJECTED
                                                                 (final)
```

Cada transición valida el estado actual con `EnsureStatus`; si no corresponde, lanza
`INVALID_PAYMENT_STATUS_TRANSITION`. Ejemplos: no se puede aprobar un pago rechazado ni reembolsar
uno que no está aprobado.

**Dos formas de crear un pago (Factory Methods):**

| Método | Quién | Qué pasa |
|--------|-------|----------|
| `Payment.ReportWithReceipt(...)` | Cliente | Pagó por QR desde su banco. Exige imagen válida. Adjunta el comprobante y queda en `IN_REVIEW`. Guarda `amount` (lo esperado) y opcionalmente `reportedAmount` (lo que el cliente dice que pagó). |
| `Payment.RegisterInPerson(...)` | Admin | Pago recibido en el lavadero. Usa un comprobante "marcador" (`manual:registrado-por-admin`) y queda **aprobado de una vez**. |

**Transiciones:**

- `AttachReceipt` — solo en `PENDING`.
- `SubmitForReview` — exige al menos un comprobante.
- `Approve(admin, now)` — guarda quién y cuándo, marca el comprobante como revisado y registra el
  evento `PaymentApproved`.
- `Reject(admin, reason, now)` — exige motivo (máx. 200 caracteres), lo copia al comprobante y
  registra `PaymentRejected`.
- `Refund(now)` — solo desde `APPROVED`; registra `PaymentRefunded`. Si el cliente vuelve a
  pagar, es un **pago nuevo**.

Un detalle de diseño: el pago **no guarda el medio** (Nequi, efectivo…). Se llega a él a través de
la cuenta (`PaymentAccountId`). Así es imposible tener un pago que diga "Nequi" y apunte a la
cuenta de Daviplata.

#### `PaymentReceipt` — comprobante

Entidad dentro del aggregate `Payment` (sus métodos son `internal`: solo `Payment` los usa). Guarda
la imagen (`FileUrl`), la referencia de la transacción, el monto reportado, quién lo subió y quién
lo revisó. Es una entidad aparte para que un comprobante corregido **no borre el anterior**.
`LatestReceipt` es el que se revisa y se muestra.

#### `PaymentStatus` y `PaymentStatusCodes`

Enum de estados y su traducción a códigos de texto (`PENDING`, `IN_REVIEW`…), que es lo que viaja
en el JSON. `IsOpen()` indica si el pago todavía espera decisión (`PENDING` o `IN_REVIEW`).

#### `PaymentPolicy` — reglas que cruzan aggregates

Funciones puras con reglas que dependen de datos de otros servicios:

- `IsBookingPayable(status)` — solo se paga una reserva `CONFIRMED`, `IN_PROGRESS` o `COMPLETED`.
- `AmountDue(total, descuentos)` — lo que falta por pagar: total menos cupones, nunca negativo.

### 4.3 Cuentas de pago (`Domain/PaymentAccounts`)

- `PaymentMethodType` — catálogo: `EFECTIVO`, `NEQUI`, `DAVIPLATA`, `TRANSFERENCIA`. El campo
  `RequiresReceipt` decide si el cliente puede reportar a ese medio (el efectivo no: lo registra
  el admin).
- `PaymentAccount` — una cuenta del lavadero: titular, número, QR (imagen validada con
  `ImageSource`), instrucciones y si está activa. El efectivo también tiene cuenta (sin QR) para que
  todo pago apunte siempre a una cuenta, sin casos especiales.

### 4.4 Promociones (`Domain/Promotions`)

#### `Promotion` — aggregate

Es a la vez una **tarjeta de marketing** (precio de referencia, duración, ícono, beneficios,
destacada) y un **cupón real** con:

- Tipo y valor de descuento (`DiscountType` + `DiscountValue`).
- Tope de descuento (`MaxDiscountAmount`), compra mínima (`MinPurchaseAmount`).
- Vigencia (`ValidFrom`/`ValidTo`), activa o pausada.
- Límites de uso total y por cliente.
- `RequiredPoints`: puntos necesarios para desbloquearla.

El estado que ve el admin (`active`, `scheduled`, `paused`) **se calcula**, no se guarda.

**Canje (`Redeem`)** — valida, en orden:

1. Está vigente y activa.
2. El cliente tiene los puntos requeridos.
3. No se canjeó ya en esa reserva.
4. Se cumple la compra mínima.
5. No superó el límite total de usos.
6. El cliente no superó su límite personal.

Si todo pasa, crea un `PromotionRedemption` con el descuento **congelado** y registra el evento
`PromotionRedeemed`. El número de usos no se guarda en la promoción: se cuenta a partir de los
canjes, para no tener un contador que se desincronice.

#### Patrón Strategy para los descuentos (`Promotions/Discounts`)

```
IDiscountStrategy
 ├── PercentageDiscountStrategy   subtotal × valor / 100   (valor entre 1 y 99; siempre queda 1 peso por pagar, E-07)
 └── FixedAmountDiscountStrategy  valor fijo en pesos      (valor > 0)

DiscountStrategyFactory.For(tipo) → la estrategia que corresponde
```

`Promotion.CalculateDiscount` pide la estrategia, calcula, aplica el tope `MaxDiscountAmount`, nunca
descuenta más que el subtotal y redondea a centavos. Agregar un tipo nuevo de descuento es agregar
una clase, sin tocar `Promotion`.

#### `PromotionRedemption` y `RedemptionRequest`

- `PromotionRedemption` — "esta promoción se aplicó a esta reserva y descontó X". El monto queda
  fijo aunque la promoción cambie después.
- `RedemptionRequest` — *Parameter Object* con todo lo que `Redeem` necesita saber (puntos del
  cliente, subtotal, canjes previos…). La capa de aplicación lo arma consultando repositorios; así
  la regla queda en el dominio sin que el dominio dependa de repositorios.

### 4.5 Fidelización (`Domain/Loyalty`)

#### `LoyaltyTransaction` — ledger *append-only*

Cada movimiento de puntos es una fila nueva; **nunca se edita una fila**. Cada movimiento guarda:

- `Points` con signo (`+` ganados, `−` revertidos).
- `BalanceAfter`: el saldo justo después de ese movimiento. **Es la fuente de verdad**: el saldo
  actual es el `BalanceAfter` del último movimiento.
- `Sequence`: posición 1, 2, 3… en el ledger de ese cliente, **única por cliente**.

Factory Methods:

- `Earn(...)` — puntos ganados por una reserva pagada.
- `ReverseEarned(...)` — revierte los puntos de una reserva reembolsada. Si el cliente ya no los
  tiene todos, revierte solo lo que queda: **el saldo nunca queda negativo**.

#### `LoyaltyBalance` — value object

Saldo (`Points`) + posición del último movimiento (`Sequence`). El siguiente movimiento ocupa
`Sequence + 1`. Si dos requests calculan a la vez sobre el mismo saldo, los dos intentan la misma
posición y la base rechaza al segundo (ver [sección 11](#11-consistencia-y-concurrencia)).

#### `LoyaltyMovementType`

`EARNED (+)`, `REDEEMED (−, reservado)`, `EXPIRED (−)`, `ADJUSTED (+)`, `REVERSED (−)`.

---

## 5. Capa de aplicación

Orquesta los casos de uso: busca datos, valida precondiciones externas, delega la regla al dominio,
guarda y publica. **No contiene reglas de negocio** propias del aggregate.

### 5.1 Puertos de entrada (`Ports/In`) y sus implementaciones

| Puerto | Implementación | Casos de uso |
|--------|----------------|--------------|
| `IPaymentCommandUseCases` | `PaymentCommandService` | Reportar, registrar en caja, aprobar, rechazar, reembolsar |
| `IPaymentQueryUseCases` | `PaymentQueryService` | Mis pagos, un pago mío, cola del admin, un pago, monto a pagar de una reserva |
| `IPaymentAccountUseCases` | `PaymentAccountService` | Cuentas para el cliente, todas (admin), medios, crear, editar |
| `IPromotionAdminUseCases` | `PromotionAdminService` | Listar, métricas, crear, editar, pausar, borrar (lógico) |
| `ICustomerPromotionUseCases` | `CustomerPromotionService` | Promociones disponibles para el cliente y canje de cupón |
| `ILoyaltyUseCases` | `LoyaltyService` | Saldo de puntos del cliente |

Comandos (cambian estado) y consultas (solo leen) están separados (**CQRS ligero**): las consultas
no necesitan la Unit of Work ni el bus de eventos.

### 5.2 Puertos de salida (`Ports/Out`)

| Puerto | Para qué | Implementado en |
|--------|----------|-----------------|
| `IPaymentRepository` | Guardar/leer pagos, saber si una reserva tiene pago abierto o aprobado, control antifraude de referencia | `PaymentRepository` (EF Core) |
| `IPaymentAccountRepository` | Cuentas y medios de pago | `PaymentAccountRepository` |
| `IPromotionRepository` | Promociones | `PromotionRepository` |
| `IPromotionRedemptionRepository` | Canjes: contar, sumar descuentos de una reserva, estadísticas | `PromotionRedemptionRepository` |
| `ILoyaltyLedgerRepository` | Saldo actual, puntos netos de una reserva, agregar movimientos | `LoyaltyLedgerRepository` |
| `IUnitOfWork` | Confirmar todo lo del request en una transacción | `EfUnitOfWork` |
| `IBookingDirectory` | Consultar reservas en booking-service | `BookingServiceDirectory` (HTTP) |
| `IIntegrationEventPublisher` | Publicar eventos | `RabbitMqIntegrationEventPublisher` o `NullIntegrationEventPublisher` |

### 5.3 Colaboradores compartidos

| Clase | Responsabilidad |
|-------|-----------------|
| `BookingPaymentGuard` | Precondiciones que dependen de datos fuera del aggregate: reserva pagable, sin pago aprobado, sin pago abierto, cuenta usable, referencia no reutilizada en otra reserva. |
| `AmountDueCalculator` | Lo que falta por pagar: total de la reserva − cupones canjeados (`PaymentPolicy.AmountDue`). Lo usan el pago, el canje de cupones y la consulta `amount-due`, así **todos usan la misma cifra**. |
| `LoyaltyRewardService` | Acredita los puntos de una reserva al aprobar (idempotente) y los revierte al reembolsar. Solo agrega movimientos; los confirma la Unit of Work del caso de uso. |
| `PaymentDtoAssembler` | Construye `PaymentDto` (pago + último comprobante + cuenta + reserva). Comandos y consultas devuelven exactamente la misma forma. |
| `PaymentAccountReader` | Arma `PaymentAccountDto` juntando cuenta y medio de pago. |

### 5.4 El guion de todo caso de uso que cambia estado

`PaymentCommandService` sigue siempre los mismos 6 pasos:

```
1. Leer lo necesario          → reserva (booking-service), pago, cuenta
2. Validar precondiciones     → BookingPaymentGuard (ANTES de tocar el aggregate)
3. Delegar la regla           → Payment.ReportWithReceipt / Approve / Reject / Refund
4. Mantener puntos alineados  → LoyaltyRewardService (acreditar o revertir)
5. Confirmar todo junto       → IUnitOfWork.CommitAsync (una transacción)
6. Publicar eventos           → solo DESPUÉS de confirmar (PublishAndClearAsync)
```

¿Por qué publicar después de confirmar? Si se publicara antes y la base fallara, se avisaría al
cliente de algo que nunca quedó guardado.

### 5.5 Contratos: `*Command`, `*Dto` y `*Request`

- **`*Command`** (Application): lo que entra a un caso de uso. Ej. `ReportPaymentCommand`.
- **`*Dto`** (Application): lo que sale. Se serializa tal cual en el JSON, así que **sus nombres de
  campo son contrato** con la web y el móvil. Ej. `PaymentDto`, `AmountDueDto`.
- **`*Request`** (Api): cómo llega el cuerpo HTTP. El controller lo traduce a `*Command`.
- Los modelos de los puertos de salida (ej. `BookingInfo`) **nunca** salen en una respuesta: si
  booking-service cambia su JSON, la web no se entera.

### 5.6 Identidad: `Caller`

Los casos de uso no reciben tokens ni `HttpContext`: reciben `Caller(UserId)`. El token para
llamar a booking-service lo agrega la infraestructura de forma transparente.

---

## 6. Capa de infraestructura

### 6.1 Persistencia con EF Core

- **`PaymentDbContext`** mapea los aggregates a las tablas de los esquemas `payment` y `promotion`.
  Las configuraciones (`Configurations/`) definen columnas, conversiones y relaciones.
- **Columnas de auditoría** (`AuditColumns`): `created_at`, `updated_at`, `deleted_at`,
  `deleted_by` y `row_version` existen en las tablas pero **no en el dominio** (son propiedades
  *shadow* de EF). El dominio queda limpio.
- **Borrado lógico:** un filtro global oculta las filas con `deleted_at` lleno. Borrar una promoción
  solo marca `deleted_at`/`deleted_by`.
- **Repositorios:** traducen las necesidades de los casos de uso a consultas LINQ. Ejemplo: el saldo
  de puntos es el `BalanceAfter` del movimiento con mayor `sequence_no` del cliente.

### 6.2 `EfUnitOfWork`

Todos los repositorios del request comparten el mismo `DbContext` (Scoped), así un solo
`SaveChanges` confirma todo **en una transacción**. Antes de guardar:

- Toda fila modificada registra `updated_at`.
- Toda fila nueva nace con `row_version = 1` y cada UPDATE lo incrementa (**concurrencia
  optimista**: el UPDATE incluye el valor anterior en el WHERE).
- Un aggregate con eventos pendientes cuenta como modificado aunque sus datos no cambien. Ejemplo:
  una promoción que consumió un uso. Así dos canjes simultáneos chocan en vez de saltarse el límite.

Si la base detecta un choque (concurrencia o índice único) se traduce a **409 `DATA_CONFLICT`**.

### 6.3 Catálogos: `CatalogIds` y `PaymentStatusIds`

Los enums del dominio se guardan como IDs de tablas de catálogo:

- **`CatalogIds`** — tipos de descuento y de movimiento de puntos. Sus IDs se **leen por código al
  arrancar**, porque SQL Server puede saltar valores IDENTITY (pasó: `PACKAGE` quedó con ID 102).
  Si la base no responde al arrancar, el servicio no arranca (mejor que escribir IDs equivocados).
- **`PaymentStatusIds`** — estados de pago con IDs **fijos** (1 a 5). `APPROVED = 3` está escrito en
  el índice único de la base; las migraciones 015 y 019 verifican que coincida.

### 6.4 Integración con booking-service (`Infrastructure/Booking`)

- **`BookingServiceDirectory`** — cliente HTTP tipado. Llama a `bookings/{id}` (cliente),
  `admin/bookings/{id}` (admin) y `bookings/me`. Es una **capa anticorrupción**: traduce el JSON de
  booking a `BookingInfo`; si ese JSON cambia, solo se toca este archivo.
  - 404 → la reserva no existe o no es del que llama (`null`).
  - Error de red, tiempo agotado (5 s) o 5xx → `ServiceUnavailableException` → **503
    `BOOKING_SERVICE_UNAVAILABLE`**.
- **`AccessTokenForwardingHandler`** — agrega a cada llamada el token del usuario del request
  (propagación de identidad). booking-service aplica así sus propios permisos.

### 6.5 Mensajería (`Infrastructure/Messaging`)

- **`RabbitMqIntegrationEventPublisher`** — publica en el exchange topic `carwash.events` con el
  mismo sobre que los servicios Java: `{eventId, eventType, aggregateId, occurredAt, version,
  payload}`. Mensajes persistentes. Comparte una conexión y abre un canal por mensaje.
- **`NullIntegrationEventPublisher`** — patrón *Null Object*: si `MESSAGING_ENABLED` no es `true`,
  publicar no hace nada y los casos de uso no tienen que preguntar si hay bus.

---

## 7. Capa de API

### 7.1 Arranque (`Program.cs`)

1. Carga variables de `lavarapido-infra/.env` (`EnvFile`); las variables de entorno mandan.
2. Puerto `PAYMENT_PORT` (por defecto 3005). Arma la cadena de conexión con `DB_*` si no viene.
3. Límite de cuerpo de 8 MB (las imágenes viajan en base64).
4. JWT, CORS, controllers, Swagger, manejador global de errores.
5. `AddApplication()` y `AddInfrastructure()` registran casos de uso y adaptadores.
6. Registra el reloj (`TimeProvider.System`) y el proveedor del token del request.
7. Carga los IDs de catálogos (`InitializeInfrastructureAsync`).
8. Expone `/health` y `/actuator/health`.

### 7.2 Autenticación y roles

- JWT **HS256** emitido por security-service: mismo secreto (`JWT_SECRET`), emisor y audiencia.
- El rol sale del claim `roles` y el usuario del claim `sub`.
- `HttpContextCallerExtensions.GetCaller()` convierte el `sub` en `Caller(UserId)`.

### 7.3 Endpoints

Todas las rutas empiezan con `/api/v1`.

**Cliente (`CLIENT`)**

| Método | Ruta | Qué hace | Respuesta |
|--------|------|----------|-----------|
| GET | `/payment-accounts` | Cuentas activas que reciben comprobante (con QR). *Cualquier usuario autenticado.* | `PaymentAccountDto[]` |
| POST | `/payments` | Reportar un pago `{bookingId, paymentAccountId, transactionReference, receiptImage, reportedAmount?}` | 201 `PaymentDto` (`IN_REVIEW`) |
| GET | `/payments/me` | Pagos de mis reservas (más reciente primero) | `PaymentDto[]` |
| GET | `/payments/{id}` | Un pago mío (404 si es de otro) | `PaymentDto` |
| GET | `/loyalty/balance` | Mi saldo de puntos | `{points}` |
| GET | `/loyalty/promotions` | Promociones vigentes, marcando cuáles desbloqueé | `PromotionForCustomerDto[]` |
| POST | `/loyalty/redeem` | Canjear cupón `{bookingId, code}` | `{promotionId, promotionCode, promotionName, discountAmount, newTotal}` |
| DELETE | `/loyalty/redemptions/{bookingId}/{promotionId}` | Quitar el cupón (E-08): solo el dueño y sin pago en revisión ni aprobado (si no, 409). Una sola promoción por reserva (E-09, `PROMOTION_ONE_PER_BOOKING`) | `{bookingId, bookingTotal, appliedDiscounts, amountDue}` |

**Admin (`ADMIN`)**

| Método | Ruta | Qué hace | Respuesta |
|--------|------|----------|-----------|
| GET | `/admin/payments?status=` | Cola de pagos (máx. 200), filtro opcional por estado | `PaymentDto[]` |
| GET | `/admin/payments/{id}` | Un pago | `PaymentDto` |
| GET | `/admin/payments/amount-due?bookingId=` | Lo que se cobraría por esa reserva | `{bookingId, bookingTotal, appliedDiscounts, amountDue}` |
| POST | `/admin/payments` | Pago en caja `{bookingId, paymentAccountId, transactionReference?}` | 201 `PaymentDto` (`APPROVED`) |
| POST | `/admin/payments/{id}/approve` | Aprobar | `PaymentDto` |
| POST | `/admin/payments/{id}/reject` | Rechazar `{reason}` | `PaymentDto` |
| POST | `/admin/payments/{id}/refund` | Reembolsar | 204 |
| GET | `/admin/payment-accounts` | Todas las cuentas (incluidas inactivas) | `PaymentAccountDto[]` |
| GET | `/admin/payment-methods` | Medios de pago | `PaymentMethodDto[]` |
| POST | `/admin/payment-accounts` | Crear cuenta | 201 `PaymentAccountDto` |
| PUT | `/admin/payment-accounts/{id}` | Editar cuenta | `PaymentAccountDto` |
| GET | `/admin/promotions` | Promociones | `PromotionDto[]` |
| GET | `/admin/promotions/metrics` | Canjes y ahorro total | `{redemptions, savings, conversion}` |
| POST | `/admin/promotions` | Crear | 201 `PromotionDto` |
| PUT | `/admin/promotions/{id}` | Editar | `PromotionDto` |
| PATCH | `/admin/promotions/{id}/active` | Pausar / reanudar `{active}` | `PromotionDto` |
| DELETE | `/admin/promotions/{id}` | Borrado lógico | 204 |

**Salud:** `GET /health` y `GET /actuator/health` (sin autenticación).

### 7.4 Forma de `PaymentDto`

```json
{
  "id": 12,
  "status": "IN_REVIEW",
  "amount": 45000,
  "processedAtUtc": null,
  "rejectionReason": null,
  "transactionReference": "M18492048",
  "reportedAmount": 45000,
  "receiptImage": "data:image/png;base64,...",
  "reportedAtUtc": "2026-10-08T15:30:00Z",
  "reportedBy": 3,
  "account": { "id": 2, "methodCode": "NEQUI", "methodName": "Nequi", "accountHolder": "...", "...": "..." },
  "booking": { "id": 7, "code": "RES-7", "status": "CONFIRMED", "total": 50000, "...": "..." }
}
```

- `amount` = lo que se espera cobrar (calculado por el servidor).
- `reportedAmount` = lo que el cliente dice haber pagado (`null` si no lo indicó). El admin compara
  ambos antes de aprobar.
- `receiptImage` = `null` en pagos registrados en caja.

---

## 8. Flujos completos paso a paso

### 8.1 El cliente reporta un pago por QR

```
Cliente  ──POST /payments──►  PaymentsController
                                   │  ReportPaymentCommand + Caller
                                   ▼
                          PaymentCommandService.ReportAsync
  1. booking = booking-service: bookings/{id}           (404 si no es suya)
  2. Guard: reserva pagable · sin pago aprobado · sin pago abierto
  3. Guard: cuenta activa y que exige comprobante (no efectivo)
  4. Guard: la referencia no se usó en OTRA reserva     (409 TRANSACTION_REFERENCE_REUSED)
  5. monto = AmountDueCalculator (total − cupones)
  6. Payment.ReportWithReceipt(...)  → valida imagen → IN_REVIEW
  7. Commit
  8. (sin eventos: todavía no hay decisión)
  ◄── 201 PaymentDto
```

### 8.2 El admin aprueba

```
Admin ──POST /admin/payments/{id}/approve──►
  1. payment = repositorio                              (404 si no existe)
  2. Guard: la reserva no tiene otro pago aprobado      (409 PAYMENT_ALREADY_APPROVED)
  3. booking = booking-service: admin/bookings/{id}     (sin reserva no se sabe a quién dar puntos)
  4. Guard: la reserva sigue pagable                    (409 BOOKING_NOT_PAYABLE si se canceló)
  5. payment.Approve(admin, ahora)                      → evento PaymentApproved
  6. LoyaltyRewardService.CreditForBooking               → movimiento EARNED (si aplica)
  7. Commit  (pago + puntos en la misma transacción)
  8. Publica payment.confirmed → notification-service avisa al cliente
```

La acreditación es **idempotente**: si la reserva ya tiene sus puntos vigentes, no los vuelve a
dar.

### 8.3 El admin rechaza

```
  1. payment = repositorio
  2. payment.Reject(admin, motivo, ahora)               → evento PaymentRejected
  3. booking = booking-service (solo para el aviso; si no existe, el rechazo sigue siendo válido)
  4. Commit
  5. Publica payment.rejected (incluye reason)
```

Después del rechazo, el cliente puede reportar un pago nuevo para la misma reserva (y puede reusar
la misma referencia, porque es la misma reserva).

### 8.4 El admin registra un pago en caja

```
  1. booking = booking-service: admin/bookings/{id}
  2. Guard: pagable y sin pago aprobado  (aquí SÍ se permite que haya uno en revisión)
  3. Guard: cuenta activa (puede ser efectivo)
  4. Guard: referencia no reutilizada (si se indicó)
  5. monto = AmountDueCalculator
  6. Payment.RegisterInPerson(...)                      → APPROVED directo, evento PaymentApproved
  7. Acredita puntos
  8. Commit + publica payment.confirmed
```

Si había un pago del cliente en revisión, el que se apruebe segundo choca con
`PAYMENT_ALREADY_APPROVED`.

Antes de registrar, la pantalla consulta `GET /admin/payments/amount-due?bookingId=` para mostrar
el monto exacto que se va a cobrar.

### 8.5 El admin reembolsa

```
  1. payment = repositorio
  2. payment.Refund(ahora)                              → REFUNDED, evento PaymentRefunded
  3. LoyaltyRewardService.RevokeForBooking               → movimiento REVERSED (nunca deja saldo negativo)
  4. Commit  (reembolso + reversión juntos)
  5. Publica payment.refunded
```

Si después la reserva se paga de nuevo, sus puntos netos son 0 y se acreditan otra vez.

### 8.6 El cliente canjea un cupón

```
Cliente ──POST /loyalty/redeem {bookingId, code}──►
  1. booking = booking-service (debe ser suya)
  2. Guard: pagable, sin pago aprobado y SIN pago abierto (con un pago reportado el monto ya quedó fijo)
  3. promotion = por código                              (404 PROMOTION_NOT_FOUND)
  4. Arma RedemptionRequest: puntos del cliente, subtotal (AmountDue), canjes previos
  5. promotion.Redeem(request)  → valida las 6 reglas, calcula descuento con la Strategy
  6. Guarda el PromotionRedemption + Commit
  7. Publica payment.promotion_redeemed
  ◄── {discountAmount, newTotal}
```

Canjear **no gasta puntos**: los puntos solo desbloquean la promoción. El descuento queda guardado
y `AmountDueCalculator` lo resta cuando se paga.

---

## 9. Base de datos

Las migraciones son **Liquibase en SQL puro** (`db/changelog/`), registradas en
`db.changelog-master.yaml`. Usan sus propias tablas de control (`DATABASECHANGELOG_PAYMENT`) para no
chocar con otros servicios en la base compartida.

### Esquema `payment`

| Tabla | Qué guarda | Reglas en la base |
|-------|------------|-------------------|
| `payment_method_type` | Catálogo de medios | código único |
| `payment_account` | Cuentas del lavadero | FK al medio |
| `payment_status` | Catálogo de estados (IDs fijos 1–5) | `APPROVED` = 3 verificado |
| `payment` | Intentos de pago | `amount > 0`; **índice único filtrado**: un solo `APPROVED` por reserva |
| `payment_receipt` | Comprobantes | `reviewed_at` y `reviewed_by` siempre juntos |
| `loyalty_movement_type` | Catálogo de movimientos | código único |
| `loyalty_transaction` | Ledger de puntos | `points ≠ 0`, `balance_after ≥ 0`, `(customer_id, sequence_no)` único |

### Esquema `promotion`

| Tabla | Qué guarda | Reglas en la base |
|-------|------------|-------------------|
| `discount_type` | `PERCENT`, `FIXED` | código único |
| `promotion` | Promociones | código único, vigencia válida, límites positivos |
| `booking_promotion` | Canjes | una vez por (reserva, promoción); `applied_amount > 0` |
| `promotion_service`, `promotion_customer` | Restricciones por servicio/cliente | **no modeladas todavía** |

Todas las tablas tienen `created_at/by`, `updated_at/by`, `deleted_at/by` y `row_version`.

### Migraciones importantes

| Archivo | Qué hace |
|---------|----------|
| 001–014 | Esquemas y tablas |
| 015 | Datos de referencia (medios, estados, tipos) y verificación de `APPROVED = 3` |
| 016 | Imágenes como data URL (`NVARCHAR(MAX)`) mientras no haya almacenamiento de archivos |
| 017–018 | Campos de paquete y puntos requeridos en promociones |
| 019 | Verifica los IDs de catálogo |
| 020 | Tipo `REVERSED` y `sequence_no` único por cliente en el ledger |

---

## 10. Eventos que publica

Exchange `carwash.events` (topic), activo con `MESSAGING_ENABLED=true`. Los escucha
notification-service.

| Evento | Routing key | Cuándo | Payload |
|--------|-------------|--------|---------|
| `PaymentConfirmed` | `payment.confirmed` | Pago aprobado (también en caja) | `paymentId, bookingId, customerUserId, amount` |
| `PaymentRejected` | `payment.rejected` | Pago rechazado | `... + reason` |
| `PaymentRefunded` | `payment.refunded` | Pago reembolsado | `paymentId, bookingId, customerUserId, amount` |
| `PromotionRedeemed` | `payment.promotion_redeemed` | Cupón canjeado | `customerUserId, bookingId, bookingCode, promotionCode, promotionName, discountAmount` |

**Cómo nace un evento:** el aggregate registra un *evento de dominio* (`PaymentApproved`). El caso
de uso confirma la transacción, lo traduce a *evento de integración* (`PaymentIntegrationEvents`) y
lo publica. El dominio no sabe que existe RabbitMQ.

---

## 11. Consistencia y concurrencia

¿Qué pasa si dos personas hacen algo a la vez? El servicio tiene **tres capas de defensa**:

1. **Validación en la aplicación** — por ejemplo, `EnsureNoApprovedPaymentAsync` antes de aprobar.
   Cubre el caso normal y da un mensaje claro.
2. **Concurrencia optimista (`row_version`)** — si dos requests modifican la misma fila, el segundo
   no encuentra la versión que leyó y falla. Incluye aggregates que solo registraron eventos (una
   promoción que consumió un uso).
3. **Restricciones de la base** — índices únicos que son la garantía final:
   - Un solo pago aprobado por reserva.
   - Un cupón una vez por reserva.
   - Una posición del ledger por cliente: dos movimientos de puntos simultáneos no pueden dejar un
     saldo equivocado.

Cualquier choque de las capas 2 y 3 llega al cliente como **409 `DATA_CONFLICT`** ("recarga e
intenta de nuevo").

---

## 12. Errores

Todas las respuestas de error siguen **RFC 9457 (ProblemDetails)** con un campo `code` estable,
igual que los servicios Java. La web y el móvil traducen `API_ERRORS.<code>`.

```json
{ "status": 409, "title": "Conflict", "detail": "La reserva ya tiene un pago aprobado.", "code": "PAYMENT_ALREADY_APPROVED" }
```

El código HTTP se decide por el **tipo de excepción** (`GlobalExceptionHandler`):

| Excepción | HTTP | Ejemplos de `code` |
|-----------|------|--------------------|
| `DomainException` | 400 | `INVALID_RECEIPT_FILE`, `INVALID_PAYMENT_STATUS_TRANSITION`, `PROMOTION_NOT_REDEEMABLE` |
| `InvalidRequestException` | 400 | `INVALID_PAYMENT_ACCOUNT`, `INVALID_PAYMENT_METHOD` |
| `NotFoundException` | 404 | `PAYMENT_NOT_FOUND`, `BOOKING_NOT_FOUND`, `PROMOTION_NOT_FOUND` |
| `ConflictException` | 409 | `PAYMENT_ALREADY_APPROVED`, `PAYMENT_ALREADY_REPORTED`, `BOOKING_NOT_PAYABLE`, `TRANSACTION_REFERENCE_REUSED`, `DATA_CONFLICT` |
| `ServiceUnavailableException` | 503 | `BOOKING_SERVICE_UNAVAILABLE` |
| `UnauthorizedAccessException` | 401 | `UNAUTHORIZED` |
| Cualquier otra | 500 | sin detalles internos |

Convención: las reglas de un aggregate lanzan `DomainException` (códigos en `DomainErrorCodes`);
los casos de uso lanzan subclases de `ApplicationLayerException` (códigos en `ErrorCodes`).

---

## 13. Configuración

Las variables salen de `lavarapido-infra/.env` (o del entorno, que tiene prioridad).

| Variable | Por defecto | Uso |
|----------|-------------|-----|
| `PAYMENT_PORT` | `3005` | Puerto HTTP |
| `ConnectionStrings__PaymentDb` | — | Si no viene, se arma con `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USERNAME`, `DB_PASSWORD` |
| `JWT_SECRET` | **obligatoria** | Secreto HS256 compartido con security-service |
| `JWT_ISSUER` / `JWT_AUDIENCE` | `lavarapido-security-service` / `lavarapido-api` | Validación del token |
| `CORS_ALLOWED_ORIGINS` | `http://localhost:4200` | Orígenes separados por coma |
| `BOOKING_SERVICE_URL` | `http://localhost:3003/api/v1` | Base de booking-service |
| `MESSAGING_ENABLED` | `false` | Activa RabbitMQ |
| `RABBITMQ_HOST/PORT/USERNAME/PASSWORD` | `localhost/5672/guest/guest` | Conexión al bus |

---

## 14. Pruebas

| Proyecto | Qué prueba | Cuántas |
|----------|------------|---------|
| `PaymentService.Domain.UnitTests` | Máquina de estados, políticas, promociones, ledger | 97 |
| `PaymentService.Application.UnitTests` | Casos de uso con **dobles en memoria** de los puertos (`Fakes/InMemoryPorts.cs`) | 15 |
| `PaymentService.Infrastructure.IntegrationTests` | Consultas EF, concurrencia real contra SQL Server (base aislada, puerto 1434) | 7 (se omiten sin `PAYMENT_INTEGRATION_DB`) |
| `scripts/e2e/payment_flow.py` | Flujo completo por el gateway con todos los servicios arriba | — |

```bash
dotnet test                                    # unitarias
python -X utf8 scripts/e2e/payment_flow.py     # punta a punta (backend completo levantado)
```

Los dobles en memoria imitan a EF: los cambios quedan pendientes hasta que la Unit of Work
confirma, así las pruebas detectan si algo se guarda sin pasar por el commit.

---

## 15. Cómo lo consumen la web y el móvil

| | Web (Angular) | Móvil (React Native) |
|---|---|---|
| Cliente HTTP | `core/services/payments-api.ts`, `loyalty-api.ts`, `promotions-api.ts` | `core/services/payments/PaymentService.ts` |
| Pantalla del cliente | `features/client/pages/payment` | `modules/client/screens/ClientPaymentScreen.tsx` |
| Panel del admin | `features/admin/pages/payments` + `payments-store.ts` | `modules/admin/screens/AdminPaymentsScreen.tsx` + `usePayments.ts` |
| Modal de revisión | `shared/dialogs/payment-review-modal` | `modules/admin/components/payments/PaymentReviewModal.tsx` |
| Pago en caja | `.../payments/components/payment-modal` | `.../payments/ManualPaymentModal.tsx` |
| Cuentas y QR | `.../settings/components/payment-methods` | `useSettings.ts` + `qrPicker.ts` |

Ambos pasan por el api-gateway. Los estados del backend se agrupan en la UI así: `PENDING` e
`IN_REVIEW` → *pendiente*; `APPROVED` → *aprobado*; `REJECTED` → *rechazado*; `REFUNDED` →
*reembolsado*.

---

## 16. Limitaciones conocidas y pendientes

| Tema | Situación | Impacto |
|------|-----------|---------|
| **Sin Outbox** | Si RabbitMQ falla después del commit, el evento se pierde (queda en el log). | El cliente podría no recibir el aviso. Solución: patrón Outbox. |
| **Antifraude de referencia** | Es *check-then-act* sin índice único. | Dos reportes simultáneos con la misma referencia en reservas distintas podrían pasar. |
| **Imágenes en la base** | Comprobantes y QR como base64 en `NVARCHAR(MAX)`. | Tablas grandes y listados pesados. Solución: almacenamiento de archivos. |
| **Restricciones de promociones** | `is_public`, `min_completed_booking`, por servicio o cliente: no modeladas. | Toda promoción se comporta como pública y sin restricciones. |
| **Traducciones del frontend** | Los códigos de error de pagos aún no están en los JSON de idiomas; algunos textos nuevos están temporalmente en `payment-texts.ts` (web). | Algunos errores se ven como "error inesperado". |

---

## 17. Glosario

| Término | Significado |
|---------|-------------|
| **Aggregate** | Grupo de objetos que se modifican juntos a través de una raíz que protege sus reglas (ej. `Payment` con sus comprobantes). |
| **Aggregate root** | La entidad principal del aggregate; la única que se toca desde afuera. |
| **Value object** | Objeto sin identidad, definido por sus valores (ej. `LoyaltyBalance`). |
| **Evento de dominio** | Hecho de negocio que ocurrió dentro del dominio (`PaymentApproved`). |
| **Evento de integración** | Mensaje que se publica a otros servicios (`payment.confirmed`). |
| **Puerto** | Interfaz que define qué necesita u ofrece la aplicación. |
| **Adaptador** | Implementación concreta de un puerto (EF Core, HTTP, RabbitMQ). |
| **Unit of Work** | Agrupa todos los cambios de un request en una transacción. |
| **Concurrencia optimista** | No se bloquea al leer; se detecta el choque al guardar (`row_version`). |
| **Ledger append-only** | Registro donde solo se agregan filas, nunca se editan. |
| **Capa anticorrupción** | Traductor que aísla tu modelo del modelo de otro servicio. |
| **Idempotente** | Ejecutarlo dos veces da el mismo resultado que una (ej. acreditar puntos). |
| **Composition root** | El único lugar donde se conectan todas las piezas (`Program.cs`). |
