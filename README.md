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

```
payment-service/
├── PaymentService.sln
├── Directory.Build.props        ← versión de .NET y opciones comunes (net8.0)
├── Dockerfile                   ← build multi-stage para correrlo con lavarapido-infra
├── db/changelog/...             ← migraciones Liquibase
├── src/
│   ├── PaymentService.Domain/          ← reglas de negocio puras (Common/Entity, AggregateRoot...)
│   ├── PaymentService.Application/     ← casos de uso (cuentas de pago, reporte y revisión de pagos)
│   ├── PaymentService.Infrastructure/  ← EF Core, SQL Server (repositorios y DbContext)
│   └── PaymentService.Api/             ← controllers HTTP, JWT, CORS y /health
└── tests/PaymentService.Domain.UnitTests/
```

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
5. F5 para levantar la API (escucha en http://localhost:5080).

Requiere la carga de trabajo "ASP.NET y desarrollo web" del instalador de Visual Studio.

## 2. Compilar y probar

```bash
dotnet build
dotnet test
```

Esperado: `Build succeeded` con 0 errores, y 3 pruebas pasadas.

## 3. Levantar la API y ver que responde

```bash
dotnet run --project src/PaymentService.Api
```

En otra terminal:

```bash
curl http://localhost:5080/health
```

Esperado: `{"service":"payment-service","status":"ok"}`
