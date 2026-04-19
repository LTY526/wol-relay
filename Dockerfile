# Build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Install native AOT prerequisites
RUN apt-get update && apt-get install -y clang zlib1g-dev

# Copy project file and restore dependencies
COPY WOLRelay/WOLRelay.csproj WOLRelay/
RUN dotnet restore "WOLRelay/WOLRelay.csproj"

# Copy the rest of the application code
COPY WOLRelay/ WOLRelay/

# Build and publish the application with AOT compilation
# Use the correct runtime identifier based on target architecture
WORKDIR /src/WOLRelay
RUN if [ "$TARGETARCH" = "amd64" ]; then \
        RID="linux-x64"; \
    elif [ "$TARGETARCH" = "arm64" ]; then \
        RID="linux-arm64"; \
    else \
        echo "Unsupported architecture: $TARGETARCH" && exit 1; \
    fi && \
    dotnet publish -c Release -r $RID -o /app/publish

# Runtime stage - using minimal base image for AOT
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Expose port 8080 (default for ASP.NET Core)
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["./WOLRelay"]