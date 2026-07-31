FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Tianci.OA.slnx ./
COPY src/Tianci.OA.Domain/Tianci.OA.Domain.csproj src/Tianci.OA.Domain/
COPY src/Tianci.OA.Application/Tianci.OA.Application.csproj src/Tianci.OA.Application/
COPY src/Tianci.OA.Infrastructure/Tianci.OA.Infrastructure.csproj src/Tianci.OA.Infrastructure/
COPY src/Tianci.OA.WebApi/Tianci.OA.WebApi.csproj src/Tianci.OA.WebApi/
RUN dotnet restore src/Tianci.OA.WebApi/Tianci.OA.WebApi.csproj
COPY src/ src/
RUN dotnet publish src/Tianci.OA.WebApi/Tianci.OA.WebApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN addgroup --system tianci && adduser --system --ingroup tianci tianci && mkdir -p /app/App_Data/files && chown -R tianci:tianci /app
COPY --from=build --chown=tianci:tianci /app/publish .
USER tianci
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tianci.OA.WebApi.dll"]
