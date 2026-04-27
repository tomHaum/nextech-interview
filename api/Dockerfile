FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Build on the host platform to avoid QEMU emulation issues with MSBuild
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Nextech.Api/Nextech.Api.csproj", "src/Nextech.Api/"]
RUN dotnet restore "src/Nextech.Api/Nextech.Api.csproj"
COPY . .
WORKDIR "/src/src/Nextech.Api"
RUN dotnet publish "Nextech.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Nextech.Api.dll"]
