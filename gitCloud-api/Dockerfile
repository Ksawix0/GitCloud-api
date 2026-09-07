FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /app
COPY . .
RUN dotnet restore "gitCloud-api/gitCloud-api.csproj"
RUN dotnet publish "gitCloud-api/gitCloud-api.csproj" -c $BUILD_CONFIGURATION -o /publish /p:UseAppHost=false

FROM dhi.io/aspnetcore:10 AS final
WORKDIR /app
COPY --from=build /publish .
ENTRYPOINT ["dotnet", "gitCloud-api.dll"]