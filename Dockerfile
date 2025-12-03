# Etapa base con ASP.NET Core
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Render usa este puerto
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# Etapa de build con SDK
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar todo el proyecto
COPY . .

# Restaurar paquetes
RUN dotnet restore

# Publicar la app
RUN dotnet publish -c Release -o /app/publish

# Imagen final
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# IMPORTANTE: Cambia TU_PROYECTO.dll por el nombre real
ENTRYPOINT ["dotnet", "MERCADEAFRAPHQL.dll"]