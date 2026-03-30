## 0.2.5

- Fixed plugin build glob excluding sibling projects (AutoFeed.Core, AutoFeed.Tests)
- Removed plugin build step from release workflow (requires Valheim game DLLs unavailable in CI)
- Added `Scripts/package.sh` to build and zip the Thunderstore package locally
- Bumped GitHub Actions to Node.js 24

## 0.2.4

- Replaced prebuild PowerShell manifest sync script with a release workflow
- Added automated CI tests via GitHub Actions
- Added branch protection requiring tests to pass before merging to main

## 0.2.3

- Added missing Thunderstore dependency to Jotunn
- Added manifest version script to avoid mismatch versions

## 0.2.2

- Fixed project version mismatch

## 0.2.1

- Removed unnecessary async delay in FeedAnimal
- Codebase improvements

## 0.2.0

- Changed targeted framework
- Added Jotunn dependency
- Added requirement Client/Server to have latest Minor version installed

## 0.1.0

- Added Jotunn for reference and assembly handling
- Code readability updates
- Added more details to README
- Increased default range from 5 to 10
- Rewrote global feeding delay logic
- Removed ownership check

## 0.0.2

- Updated GitHub link
