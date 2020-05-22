#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS base
WORKDIR /app

# copy csproj and restore as distinct layers
COPY . ./
RUN dotnet restore

# copy and build everything else
COPY . ./
RUN dotnet publish ./ListAllBlobsSvc/ListAllBlobsSvc.csproj -c Release -o out
ENTRYPOINT ["dotnet", "out/ListAllBlobsSvc.dll"]
