FROM mcr.microsoft.com/dotnet/sdk:10.0-preview

WORKDIR /app

COPY . .

RUN dotnet restore
RUN dotnet build -c Release

ENV S2_TOKEN=""
ENV S2_BASIN="dotnet-sdk"

CMD ["dotnet", "run", "--project", "tests/S2.StreamStore.IntegrationTests", "-c", "Release"]
