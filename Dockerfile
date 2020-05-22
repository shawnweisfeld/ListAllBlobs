#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS base
WORKDIR /app

# Copy the published web app
COPY /ListAllBlobsSvc/ /app

# Run command
# ENTRYPOINT ["dotnet", "ListAllBlobsSvc.dll"]
ENTRYPOINT ["echo", "hello"]
