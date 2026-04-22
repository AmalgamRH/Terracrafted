# Terracrafted 模组

一个通过 tModLoader 为泰拉瑞亚提供的Minecraft风格的合成与玩法增强模组，目前具有基于网格的合成系统和耐久度机制（未完成）。

## 配方模板系统

模组使用 JSON，目前支持多种配方方式以及热重载配方，比如从单个配方生成，或者从模板生成（单个定义生成多个配方）；支持无序配方，有序配方，自动适配多尺寸配方大小等；支持从外部文件夹加载

### 示例：火把模板

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

## 未来计划（还原更多Minecraft机制）

- **熔炉系统**：实现类似 Minecraft 的烧炼配方，支持燃料槽与输出槽，可自定义烧炼时间。
- **铁砧系统**：添加铁砧 UI，支持工具修复（消耗对应材料）以及物品重命名。
- **红石系统**：huh？

目标是在完全禁用原版合成表的情况下带来独特的游玩体验

## 贡献代码

1. Fork 本仓库
2. 创建功能分支（`git checkout -b feature/amazing-feature`）
3. 提交更改（`git commit -m "添加某个 amazing 功能"`）
4. 推送到分支（`git push origin feature/amazing-feature`）
5. 提交Pull Request

## 许可证

本模组采用 GPL-3.0 许可证。详情请参阅 `LICENSE.txt`。

