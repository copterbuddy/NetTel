FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5131

ENV ASPNETCORE_URLS=http://+:5131

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY NetTel.csproj .
RUN dotnet restore NetTel.csproj
COPY . .
RUN dotnet build NetTel.csproj -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish NetTel.csproj -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NetTel.dll"]
# ENTRYPOINT ["dotnet", "RUN"]