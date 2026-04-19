# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY WOLRelay/WOLRelay.csproj WOLRelay/
RUN dotnet restore "WOLRelay/WOLRelay.csproj"

# Copy the rest of the application code
COPY WOLRelay/ WOLRelay/

# Build and publish the application with AOT compilation
WORKDIR /src/WOLRelay
RUN dotnet publish -c Release -o /app/publish

# Runtime stage - using minimal base image for AOT
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Expose port 8080 (default for ASP.NET Core)
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["./WOLRelay"]