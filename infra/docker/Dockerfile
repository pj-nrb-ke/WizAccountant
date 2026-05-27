FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/WizAccountant.Contracts/ WizAccountant.Contracts/
COPY src/WizAccountant.Api/ WizAccountant.Api/
RUN dotnet publish WizAccountant.Api/WizAccountant.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection=Data Source=/data/wizaccountant.db
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "WizAccountant.Api.dll"]
