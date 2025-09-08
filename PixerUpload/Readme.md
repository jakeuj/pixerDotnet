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
    -o ../tools
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
    -o ../tools
```