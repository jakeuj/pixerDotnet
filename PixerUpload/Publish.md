# PixerUpload
程式需要將 PixerUpload 打包到 electron 內呼叫更新工具

## Publish
雙作業系統的韌體檔與預設圖片是共用的，差異應該只有執行檔

實測可以用以下參數發佈
* --self-contained true
* /p:PublishSingleFile=true
* /p:PublishTrimmed=true

### osx

```bash
dotnet publish PixerUpload.csproj \
    -c Release \
    -r osx-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true \
    -o ../electron/assets
```

### win

```bash
dotnet publish PixerUpload.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true \
    -o ../electron/assets
```