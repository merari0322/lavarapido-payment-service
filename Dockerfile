# syntax=docker/dockerfile:1

# ---- Build ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero los .csproj: esta capa se reutiliza mientras no cambien las dependencias.
COPY Directory.Build.props PaymentService.sln ./
COPY src/PaymentService.Domain/PaymentService.Domain.csproj src/PaymentService.Domain/
COPY src/PaymentService.Application/PaymentService.Application.csproj src/PaymentService.Application/
COPY src/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj src/PaymentService.Infrastructure/
COPY src/PaymentService.Api/PaymentService.Api.csproj src/PaymentService.Api/
RUN dotnet restore src/PaymentService.Api/PaymentService.Api.csproj

COPY src/ src/
# Las pruebas corren en CI / local (dotnet test), no en cada build de la imagen.
RUN dotnet publish src/PaymentService.Api/PaymentService.Api.csproj -c Release -o /app --no-restore

# ---- Runtime -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN useradd --system --no-create-home app
WORKDIR /app
COPY --from=build /app .

USER app
EXPOSE 3005
ENTRYPOINT ["dotnet", "PaymentService.Api.dll"]
