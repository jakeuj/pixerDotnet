# GitHub Workflows

## Electron Build Workflow

The `electron-build.yml` workflow automatically builds and packages the Electron application for Windows and macOS platforms.

### Triggers

- **Push to main branch**: Builds the app and uploads artifacts
- **Pull requests**: Builds the app to verify it compiles correctly
- **Version tags**: Automatically creates a GitHub release with binaries when you push a tag starting with 'v' (e.g., v1.0.0)

### Platforms

The workflow builds for:
- **Windows**: Creates NSIS installer (.exe)
- **macOS**: Creates DMG installer

### Usage

#### Automatic builds
Push to main branch or create a pull request to trigger builds.

#### Creating releases automatically
1. Create and push a version tag:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
2. The workflow will automatically:
   - Build the app for Windows and macOS
   - Create a GitHub release with the tag name
   - Attach the binary files to the release
   - Generate release notes automatically

#### Manual releases
You can also create releases manually on GitHub, and the workflow will build and attach binaries.

### Artifacts

Build artifacts are available for download from the Actions tab for 90 days after the build completes.

### Requirements

- The workflow uses Node.js 18
- Dependencies are installed from `electron/package.json`
- Build scripts must be defined in the package.json file

### Version Tag Format

Use semantic versioning tags starting with 'v':
- `v1.0.0` - Major release
- `v1.0.1` - Patch release
- `v1.1.0` - Minor release
- `v2.0.0-beta.1` - Pre-release
