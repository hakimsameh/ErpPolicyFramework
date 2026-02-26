# AGENTS.md

## Cursor Cloud specific instructions

This is a .NET 8 / C# 12 class library project with zero external service dependencies (no databases, Docker, or message brokers). All validation logic runs in-memory.

### Project overview

- **Solution file:** `ErpPolicyFramework.sln` (8 projects: 5 libraries, 1 console demo host, 1 test project, 1 benchmark project)
- **Tech stack:** .NET 8 SDK, xUnit 2.7, BenchmarkDotNet 0.14
- See `README.md` for full solution structure, and `docs/GETTING_STARTED.md` for onboarding.

### Key commands

| Task | Command |
|------|---------|
| Restore | `dotnet restore ErpPolicyFramework.sln` |
| Build | `dotnet build ErpPolicyFramework.sln` |
| Test (141 xUnit tests) | `dotnet test ErpPolicyFramework.sln` |
| Run demo host | `dotnet run --project src/PolicyFramework.Host` |
| Benchmarks | `dotnet run -c Release --project benchmarks/PolicyFramework.Benchmarks` |
| Full build+test+demo | `./build.sh` |

### Notes for cloud agents

- The .NET 8 SDK must be pre-installed (handled by the VM update script via `dotnet-install.sh`).
- There is no lint tool beyond the C# compiler warnings. `dotnet build` is the lint check.
- The `FaultingInventoryPolicy` in the demo host intentionally throws an exception to demonstrate resilience — the `fail:` and `warn:` log lines in demo output are expected, not errors.
- The three XML doc warnings during build (CS1574, CS1734) are pre-existing in the repo and are not actionable.
