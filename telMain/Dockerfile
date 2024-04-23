FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5131

ENV ASPNETCORE_URLS=http://+:5131

# USER app
# # FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# ARG configuration=Release
# WORKDIR /src
# COPY ["telMain.csproj", "telMain/"]
# RUN dotnet restore "telMain/telMain.csproj"
# COPY . .
# WORKDIR "/src/telMain"
# RUN pwd && ls ./
# # RUN dotnet build "telMain.csproj" -c $configuration -o /app/build
# RUN dotnet build "telMain.csproj" -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY telMain.csproj .
RUN dotnet restore telMain.csproj
COPY . .
RUN dotnet build telMain.csproj -c Release -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "telMain.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "telMain.dll"]