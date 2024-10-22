FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5035

ENV ASPNETCORE_URLS=http://+:5035

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY UserTel.csproj .
RUN dotnet restore UserTel.csproj
COPY . .
RUN dotnet build UserTel.csproj -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish UserTel.csproj -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserTel.dll"]
# ENTRYPOINT ["dotnet", "RUN"]