FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MemStack.sln ./
COPY MemStack/MemStack.csproj MemStack/
RUN dotnet restore MemStack/MemStack.csproj

COPY . .
RUN dotnet publish MemStack/MemStack.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

EXPOSE 10000

ENTRYPOINT ["dotnet", "MemStack.dll"]
