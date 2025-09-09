@echo off

REM Script to create and push a version tag for automatic release

if "%1"=="" (
    echo Usage: %0 ^<version^>
    echo Example: %0 1.0.0
    echo This will create and push tag 'v1.0.0'
    exit /b 1
)

set VERSION=%1

REM Add 'v' prefix if not present
if not "%VERSION:~0,1%"=="v" (
    set VERSION=v%VERSION%
)

echo Creating and pushing tag: %VERSION%

REM Create the tag
git tag %VERSION%

REM Push the tag to trigger the release workflow
git push origin %VERSION%

echo Tag %VERSION% created and pushed!
echo Check GitHub Actions to see the build progress.
echo A release will be created automatically when the build completes.

pause
