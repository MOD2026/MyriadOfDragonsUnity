# Social Safety Cloud Code module

This is the Phase 1 server module scaffold. It is authored but not deployed.

## Dependencies

- Unity client package: `com.unity.services.cloudcode` `2.10.4`.
- Server SDK: `Com.Unity.Services.CloudCode.Core` `0.0.5`.
- Server UGS API client: `Com.Unity.Services.CloudCode.Apis` `0.0.26`.

The Unity Registry metadata identifies `2.10.4` as a stable release compatible with Unity `2021.3` and above. Official Cloud Code documentation identifies Core and Apis as the module NuGet packages.

## Validation

```powershell
dotnet build CloudCode/SocialSafety/SocialSafety.sln --configuration Release
dotnet test CloudCode/SocialSafety/SocialSafety.ServerTests/SocialSafety.ServerTests.csproj --configuration Release --no-build
```

The Release solution configuration intentionally excludes the server test project from build/publish output.
