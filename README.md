# PurrNet Conversion Tool

Switching networking solutions mid-project is painful. The PurrNet Conversion Tool takes care of the heavy lifting for you, converting your code, prefabs, and scenes from other networking systems to [PurrNet](https://github.com/PurrNet/PurrNet).

It works through a recipe system where each source networking library has its own set of mappings and conversion logic. Right now, **FishNet** is supported out of the box, with more converters coming.

## What it does

The tool handles three layers of conversion:

**Code** uses Roslyn to parse your C# files and apply type, namespace, method, property, and attribute mappings. It also handles trickier cases like merging FishNet's separate `OnStartServer`/`OnStartClient` into PurrNet's unified `OnSpawned(bool asServer)`.

**Prefabs** finds networking components on your prefabs (like `NetworkTransform`, `NetworkAnimator`, `NetworkObject`) and swaps them out for the PurrNet equivalents, copying over relevant settings.

**Scenes** does the same for scene objects, including full `NetworkManager` conversion with transport setup and tick rate transfer.

Each step runs independently so you stay in control of the process.

## Getting started

### Install via PurrNet Package Manager

The easiest way. If you already have PurrNet installed, open the **PurrNet Package Manager** in Unity and install the Conversion Tool with a single click.

### Install via git URL

You can also add the package through the Unity Package Manager using this git URL:

```
https://github.com/PurrNet/PurrNet-Conversion-Tool.git?path=/Assets/PurrNet-Conversion
```

Or add it directly to your `Packages/manifest.json`:

```json
"dev.purrnet.conversion": "https://github.com/PurrNet/PurrNet-Conversion-Tool.git?path=/Assets/PurrNet-Conversion"
```

### Requirements

You'll need [PurrNet](https://github.com/PurrNet/PurrNet) installed in your project. The source networking library you're converting from (e.g. FishNet) also needs to be present since the tool reads its components during prefab and scene conversion.

## How to use it

Open the tool from **Tools > PurrNet > Conversion Tool**.

1. Select the networking system you're converting from in the dropdown
2. Set which folders to include for scripts, prefabs, and scenes (defaults to the entire Assets folder)
3. Run each conversion step in order: **Convert Code**, then **Convert Prefabs**, then **Convert Scenes**

The conversion log at the bottom of the window shows you what changed. A `ConversionChangelog.txt` file is also written to your project root with timestamped details of every modification.

After conversion, you can remove the old networking library from your project.

## Creating your own converter

The tool uses a recipe system that makes it straightforward to add support for other networking libraries. Each converter lives in its own folder and consists of three classes.

### 1. Mappings

Extend `NetworkSystemMappings` and define your mappings in the constructor:

```csharp
using PurrNet.ConversionTool;

public class MySystemMappings : NetworkSystemMappings
{
    public MySystemMappings()
    {
        SystemName = "MyNetworkLib";
        SystemIdentifiers = new List<string> { "MyNetworkLib" };

        NamespaceMappings = new Dictionary<string, string>
        {
            { "MyNetworkLib", "PurrNet" }
        };

        TypeMappings = new Dictionary<string, string>
        {
            { "NetConnection", "PlayerID" },
            { "NetObject", "NetworkIdentity" }
        };

        PropertyMappings = new Dictionary<string, string>
        {
            { "IsOwner", "isOwner" },
            { "IsServer", "isServer" }
        };

        MethodMappings = new Dictionary<string, string>
        {
            { "OnClientStart", "OnSpawned" }
        };

        // ... and so on for MemberMappings, MethodCallMappings,
        // AttributeMappings, AttributeParameterMappings, etc.
    }
}
```

For cases that don't fit into simple mappings, override `SpecialCaseHandler` to do custom Roslyn syntax tree transformations.

### 2. Prefab handling

Extend `NetworkPrefabHandling` and override `ConvertPrefab`:

```csharp
using PurrNet.ConversionTool;
using UnityEngine;

public class MySystemPrefabHandling : NetworkPrefabHandling
{
    public override bool ConvertPrefab(GameObject prefab)
    {
        // Find old components, add PurrNet equivalents, copy settings
        // Return true if anything was changed
        return false;
    }
}
```

### 3. Scene handling

Extend `NetworkSceneHandling` and override `ConvertSceneObject`:

```csharp
using PurrNet.ConversionTool;
using UnityEngine;

public class MySystemSceneHandling : NetworkSceneHandling
{
    public override bool ConvertSceneObject(GameObject sceneObject)
    {
        // Convert scene-specific objects like NetworkManager
        // Return true if anything was changed
        return false;
    }
}
```

### Assembly setup

Your converter folder needs its own `.asmdef` that references `PurrNet.ConversionTool` and the source networking library. Set it to **Editor only** and add a `defineConstraints` entry so it only compiles when the source library is present.

Take a look at the [FishNet converter](Assets/PurrNet-Conversion/Converters/FishNet%20Converter/) for a full working example.

### Discovery

The tool automatically discovers converters through reflection. As long as your three classes are in the same folder and extend the right base classes, they'll show up in the dropdown. No registration needed.

## Links

[PurrNet](https://github.com/PurrNet/PurrNet) | [Documentation](https://purrnet.gitbook.io/) | [Discord](https://discord.gg/purrnet)

## License

MIT
