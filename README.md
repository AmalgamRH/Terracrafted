# Terracrafted Mod

A Minecraft-style synthesis and gameplay enhancement mod provided through tModLoader for Terraria, currently featuring a grid-based synthesis system and durability mechanism (unfinished).

## Recipe Template System

The mod uses JSON, currently supporting multiple configuration methods and hot-reload configuration, such as generating from single configuration, or generating from template (single definition generates multiple configurations); supports unordered configuration, ordered configuration, automatic adaptation of multi-size configuration dimensions, etc.; supports adding external file folder configurations.

### Example: Torch Template

```json
{
    "RecipeGroups": [{
        "Template": {
            "Pattern": [
                ["{Gel}"],
                ["{Stick}"]
            ],
            "Outputs": [{"ItemId": "{TorchId}", "Amount": 1}]
        },
        "PlaceholderMappings": {
            "Material": "Name",
            "Gel": "Gel",
            "Stick": "Stick",
            "TorchId": "TorchId"
        }
    }],
    "MaterialDefinitions": [{
        "Materials": [{
            "Name": "Torch",
            "Gel": "Gel",
            "Stick": "TerraCraft:WoodenStick",
            "TorchId": "Torch"
        }]
    }]
}
```

## Future Plans (Restoring more Minecraft mechanics)

- **Furnace System**: Implement Minecraft-like smelting recipes, supporting fuel slots and output slots, customizable smelting times, etc.
- **Anvil System**: Add anvil UI, supporting tool repair (consumes corresponding materials) and item renaming.
- **Redstone System**: huh?

The goal is to bring a unique gameplay experience while completely disabling the original synthesis table

## Contribution Guide

1. Fork this repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m "Add some amazing feature"`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Submit Pull Request

## License

This mod adopts the GPL-3.0 license. For details, please refer to `LICENSE.txt`.

