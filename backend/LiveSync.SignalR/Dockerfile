FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["LiveSync.SignalR.csproj", "./"]
RUN dotnet restore "LiveSync.SignalR.csproj"

COPY . .
RUN dotnet publish "LiveSync.SignalR.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LiveSync.SignalR.dll"]
