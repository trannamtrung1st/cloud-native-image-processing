FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "cloud-native-image-processing.sln"
RUN dotnet publish "CloudNativeImageProcessing.Worker/CloudNativeImageProcessing.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Infrastructure references Microsoft.AspNetCore.App; base runtime image does not include that shared framework.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CloudNativeImageProcessing.Worker.dll"]
