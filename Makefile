SLN     := akeyless-pam.sln
LIB     := akeyless-pam/akeyless-pam.csproj
UNIT    := tests/AkeylessPam.Unit.Tests/AkeylessPam.Unit.Tests.csproj
INT     := tests/AkeylessPam.Integration.Tests/AkeylessPam.Integration.Tests.csproj
CONSOLE := TestConsole/TestConsole.csproj

.PHONY: all build build-release clean test test-unit test-integration console restore

all: build

## Build (debug)
build:
	dotnet build $(LIB)

## Build (release)
build-release:
	dotnet build $(LIB) -c Release

## Restore NuGet packages
restore:
	dotnet restore $(SLN)

## Clean all projects
clean:
	dotnet clean $(SLN)

## Run all tests
test:
	dotnet test $(SLN)

## Run unit tests only
test-unit:
	dotnet test $(UNIT)

## Run integration tests only
test-integration:
	dotnet test $(INT)

## Run the test console
console:
	dotnet run --project $(CONSOLE)

## Show available targets
help:
	@grep -E '^## ' Makefile | sed 's/## /  /'
	@echo ""
	@echo "Targets: all build build-release restore clean test test-unit test-integration console"
