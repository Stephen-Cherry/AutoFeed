# Auto Feed

Auto Feed is a Valheim mod that automatically feeds tameable creatures from nearby chests. No more dropping food on the ground — animals feed themselves as long as a chest with the right food is in range.

## Features

- **Automatic Feeding**: Tameable creatures within range of a chest containing their food will be fed automatically.
- **Configurable Range**: Set the radius creatures will search for containers.
- **Chest Filtering**: Optionally restrict feeding to specific chest types by name prefix.
- **Server-side**: Install on the server only — clients do not need the mod.

## Installation

1. Install [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) on your dedicated server.
2. Place `Narolith.AutoFeed.dll` in `BepInEx/plugins/` on the server.
3. Clients do not need to install anything.

## Configuration

Configuration is generated at `BepInEx/config/Narolith.AutoFeed.cfg` on first run.

| Parameter | Default | Description |
|-----------|---------|-------------|
| Container Range | `10` | Radius (units) in which creatures search for food containers. |
| Container Cache TTL | `5` | Seconds before the nearby-container list is refreshed for each animal. |
| Chest Prefix | `piece_chest` | Only containers whose name starts with this prefix are eligible. Leave empty to allow all containers. |
| Enabled | `true` | Enable or disable the mod. |

## Support

If you encounter any issues or have suggestions, open an issue on the [GitHub repository](https://github.com/stephen-cherry/autofeed).
