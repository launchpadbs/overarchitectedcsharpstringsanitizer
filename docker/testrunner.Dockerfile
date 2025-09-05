FROM mcr.microsoft.com/dotnet/sdk:8.0 AS testrunner
WORKDIR /src

COPY FlashAssessment.sln .
COPY src/Api/Api.csproj src/Api/
COPY src/Application/Application.csproj src/Application/
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY tests/UnitTests/UnitTests.csproj tests/UnitTests/
COPY tests/IntegrationTests/IntegrationTests.csproj tests/IntegrationTests/
RUN dotnet restore FlashAssessment.sln

COPY . .
CMD ["dotnet", "test", "FlashAssessment.sln", "-c", "Release", "--logger:trx"]


