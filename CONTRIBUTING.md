# Contributing

Thanks for helping to improve Mafesoft.RestClient! Bug reports, ideas and pull requests are welcome.

- **Bugs and proposals:** open an [issue](https://github.com/fmazzant/RestClient/issues) first, with a minimal
  example. For an API change, agree on the design in the issue before writing the code.
- **Security problems:** do not open a public issue, see [SECURITY.md](SECURITY.md).

## Build and test

Requirements: the .NET 10 SDK and the .NET 8 runtime (the tests also run on .NET Framework 4.8 on Windows).

```
dotnet build -c Release src/RestClient.sln
dotnet test -c Release tests/RestClient.Tests/RestClient.Tests.csproj
dotnet pack -c Release -o ./artifacts src/RestClient.sln
```

`dotnet pack` also runs the package validation against the last released version (see below).

The Native AOT smoke test needs the platform C++ toolchain (on Linux `clang` and `zlib1g-dev`), so it is not part of
the solution; CI runs it on every pull request:

```
dotnet publish -c Release -r linux-x64 -o ./aot tests/RestClient.AotSmokeTest/RestClient.AotSmokeTest.csproj
./aot/RestClient.AotSmokeTest
```

Trimming and AOT warnings are errors: a new API must be Native AOT compatible, or annotated.

## Public API

The public API is tracked by the [PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md):

- `PublicAPI.Shipped.txt` (next to each `.csproj`) lists the API of the last release; do not change it.
- `PublicAPI.Unshipped.txt` lists the changes since the last release. Adding, removing or changing a public member
  produces a warning (RS0016/RS0017) until the file is updated: use the code fix of your IDE.
- Package validation compares the packages with the last release on NuGet.org (`PackageValidationBaselineVersion` in
  `src/Directory.Build.props`). An intentional breaking change must be recorded in `CompatibilitySuppressions.xml`:

  ```
  dotnet pack -c Release src/RestClient.sln -p:ApiCompatGenerateSuppressionFile=true
  ```

Breaking changes are accepted only in a major version, with a note in the **Migrating to x.0** section of the
README.

## Branches and pull requests

- `main` is the default branch; work on a branch per issue (e.g. `issue/17-api-naming-cleanup`) and open the pull
  request against `main`, linking the issue (`Closes #17`).
- Keep the commits focused: one logical change per commit, with a message that explains why.
- Every pull request must build, pass the tests and the package validation, and update:
  - the tests;
  - `PublicAPI.Unshipped.txt` (and `CompatibilitySuppressions.xml` for a breaking change);
  - the README and the `[x.y.z] - Unreleased` section of [CHANGELOG.md](CHANGELOG.md)
    ([Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format).

## Releasing (maintainers)

1. Set the same `<Version>` in both `.csproj` files.
2. In `CHANGELOG.md` replace `Unreleased` with the release date (`## [3.0.0] - 2026-12-01`).
3. Move the content of `PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`.
4. Merge into `main`, then push the tag: `git tag v3.0.0 && git push origin v3.0.0`.
   The *Publish on NuGet* workflow checks the versions and the CHANGELOG section, runs the tests, publishes the
   packages and creates the GitHub Release.
5. Set `PackageValidationBaselineVersion` to the released version and delete the `CompatibilitySuppressions.xml` files.
