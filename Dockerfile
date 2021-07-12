# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS publish-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY /ImMillionaire/*.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine
WORKDIR /app
COPY --from=publish-env /app/out .
ENV ASPNETCORE_ENVIRONMENT Production
ENV Config__ApiKey=your-key-here
ENV Config__SecretKey=your-secret-here
ENTRYPOINT ["dotnet", "ImMillionaire.dll"]