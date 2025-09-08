# PixerUpload

## Publish

### osx

```bash
dotnet publish PixerUpload.csproj \
    -c Release \
    -r osx-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=false \
    /p:PublishTrimmed=true \
    /p:DeleteExistingFiles=true \
    -o ./bin/Release/net9.0/publish/osx-x64
```

### win

```bash
dotnet publish PixerUpload.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=false \
    /p:PublishTrimmed=true \
    /p:DeleteExistingFiles=true \
    -o ./bin/Release/net9.0/publish/win-x64
```