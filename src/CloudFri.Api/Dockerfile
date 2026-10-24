FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/CloudFri.Api/ ./CloudFri.Api/
RUN dotnet publish CloudFri.Api/CloudFri.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/storage
EXPOSE 8080
ENTRYPOINT ["dotnet", "CloudFri.Api.dll"]
