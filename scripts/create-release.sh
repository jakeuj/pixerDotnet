#!/bin/bash

# Script to create and push a version tag for automatic release

if [ $# -eq 0 ]; then
    echo "Usage: $0 <version>"
    echo "Example: $0 1.0.0"
    echo "This will create and push tag 'v1.0.0'"
    exit 1
fi

VERSION=$1

# Add 'v' prefix if not present
if [[ $VERSION != v* ]]; then
    VERSION="v$VERSION"
fi

echo "Creating and pushing tag: $VERSION"

# Create the tag
git tag $VERSION

# Push the tag to trigger the release workflow
git push origin $VERSION

echo "Tag $VERSION created and pushed!"
echo "Check GitHub Actions to see the build progress."
echo "A release will be created automatically when the build completes."
