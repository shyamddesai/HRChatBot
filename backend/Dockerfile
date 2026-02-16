FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HRApp.API/*.csproj HRApp.API/
COPY HRApp.Core/*.csproj HRApp.Core/
COPY HRApp.Infrastructure/*.csproj HRApp.Infrastructure/
RUN dotnet restore HRApp.API/HRApp.API.csproj

COPY . .
RUN dotnet publish HRApp.API/HRApp.API.csproj -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "HRApp.API.dll"]