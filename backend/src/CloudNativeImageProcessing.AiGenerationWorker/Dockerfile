FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "cloud-native-image-processing.sln"
RUN dotnet publish "CloudNativeImageProcessing.AiGenerationWorker/CloudNativeImageProcessing.AiGenerationWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Same shared-framework requirement as image-processing worker (Infrastructure → Microsoft.AspNetCore.App).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CloudNativeImageProcessing.AiGenerationWorker.dll"]
