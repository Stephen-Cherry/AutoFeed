## 1.0.3

- Documentation: Fixed README incorrectly stating server-only installation; mod requires both server and client

## 1.0.2

- Documentation: Incorrect tags on the mod in Thunderstore showing server-side when this is a client and server required mod as of 1.0.0

## 1.0.1

- Performance: container scan results are now cached per animal (configurable TTL, default 5s), eliminating the full `Piece.s_allPieces` scan on every AI tick
- Performance: distance calculations in container sorting now use `sqrMagnitude` instead of `Vector3.Distance`, removing redundant square root operations
- Performance: consumable lookup structure is now built once per feed attempt instead of once per container scanned, reducing GC pressure
- Fixed: `MissingReferenceException` when a cached container is destroyed during its TTL window
- Added `Container Cache TTL` config option (default `5`)

## 1.0.0

- Mod is now client and server side — all connected clients must have the mod installed
- Fixed feeding not working on dedicated servers: creature AI now runs on the ZDO owner (client or server) rather than assuming server-only execution
- Container discovery now uses Valheim's piece registry instead of physics queries, fixing containers not being found on clients
- Fixed performance issue: container queries now run at most once every 5 seconds per animal instead of every AI tick

## 0.3.2

- Fixed animals not feeding from chests — AutoFeed.Core.dll was missing from the Thunderstore package; FeedingLogic is now compiled directly into the main DLL so only one file needs to be shipped

## 0.3.1

- Updated README with server-side installation instructions and configuration table

## 0.3.0

- Mod is now server-side only — install on the server, clients no longer need it
- Feeding logic now only executes on the server, preventing double-feeding in multiplayer

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
