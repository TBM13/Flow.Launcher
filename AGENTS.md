## .NET Build & Test
- Always execute `dotnet` commands (`dotnet build`, `dotnet test`, `dotnet restore`) outside the sandbox (unsandboxed) so MSBuild Server and multi-node compilation work properly.