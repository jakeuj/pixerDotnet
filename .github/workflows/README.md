# GitHub Workflows

## Electron Build Workflow

The `electron-build.yml` workflow automatically builds and packages the Electron application for Windows and macOS platforms.

### Triggers

- **Push to main branch**: Builds the app and uploads artifacts
- **Pull requests**: Builds the app to verify it compiles correctly
- **Releases**: Builds the app and automatically attaches binaries to the GitHub release

### Platforms

The workflow builds for:
- **Windows**: Creates NSIS installer (.exe)
- **macOS**: Creates DMG installer

### Usage

1. **Automatic builds**: Push to main branch or create a pull request
2. **Creating releases**: 
   - Create a new release on GitHub
   - The workflow will automatically build and attach the binaries
   - Or push a tag starting with 'v' (e.g., `v1.0.0`)

### Artifacts

Build artifacts are available for download from the Actions tab for 90 days after the build completes.

### Requirements

- The workflow uses Node.js 18
- Dependencies are installed from `electron/package.json`
- Build scripts must be defined in the package.json file
