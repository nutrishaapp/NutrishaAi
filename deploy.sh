#!/bin/bash

echo "Starting deployment..."

# 1. Restore packages
echo "Restoring NuGet packages..."
dotnet restore "$DEPLOYMENT_SOURCE/NutrishaAI.API/NutrishaAI.API.csproj"
if [ $? -ne 0 ]; then
    echo "Failed to restore packages"
    exit 1
fi

# 2. Build
echo "Building the project..."
dotnet build "$DEPLOYMENT_SOURCE/NutrishaAI.API/NutrishaAI.API.csproj" --configuration Release
if [ $? -ne 0 ]; then
    echo "Failed to build project"
    exit 1
fi

# 3. Publish
echo "Publishing the project..."
dotnet publish "$DEPLOYMENT_SOURCE/NutrishaAI.API/NutrishaAI.API.csproj" --configuration Release --output "$DEPLOYMENT_TARGET"
if [ $? -ne 0 ]; then
    echo "Failed to publish project"
    exit 1
fi

echo "Deployment completed successfully!"
exit 0