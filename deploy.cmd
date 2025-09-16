@echo off

echo Starting deployment...

:: 1. Restore packages
echo Restoring NuGet packages...
call dotnet restore "%DEPLOYMENT_SOURCE%\NutrishaAI.API\NutrishaAI.API.csproj"
IF %ERRORLEVEL% NEQ 0 goto error

:: 2. Build
echo Building the project...
call dotnet build "%DEPLOYMENT_SOURCE%\NutrishaAI.API\NutrishaAI.API.csproj" --configuration Release
IF %ERRORLEVEL% NEQ 0 goto error

:: 3. Publish
echo Publishing the project...
call dotnet publish "%DEPLOYMENT_SOURCE%\NutrishaAI.API\NutrishaAI.API.csproj" --configuration Release --output "%DEPLOYMENT_TARGET%"
IF %ERRORLEVEL% NEQ 0 goto error

echo Deployment completed successfully!
goto end

:error
echo An error occurred during deployment.
exit /b 1

:end
exit /b 0