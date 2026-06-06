Tired of your mods not working because BepInEx needs its config modified to work?
Not knowing where to get mods because they're all over the place?
Having trouble installing them manually?

Introducing Lunaris! Which completely replaces BepInEx for seamless mod integration.
It has been specifically designed for Erenshor and lets you download, enable, disable, and manage every mod, **WHILE THE GAME IS RUNNING!**
Additionally, it also automatically updates your mods.


Lunaris uses the [Erenshor Vault](<https://erenshorvault.app>) API for browsing mods,
comes with its own modding API, but also has backwards (legacy) compatibility with BepInEx through a wrapper that integrates BepInEx mods with Lunaris.

It is heavily inspired by [Dalamud](<https://github.com/goatcorp/Dalamud>) for FFXIV.

## Installation
1. Download the latest release
2. Extract the single dll (winhttp.dll) into your Erenshor folder (next to `Erenshor.exe`)
3. Launch the game and it will automatically download the two required dlls to run Lunaris

It *should* work out of the box unless your AV flags it or windows blocks its connection.

## Using Mods
On the main menu, Lunaris shows an icon on the left side of the screen.
Click it to open the plugin installer where you can browse and install mods.

Once in-game, use the chat to interact with Lunaris:
``` 
`/lunaris plugins` Opens the plugin installer
`/lunaris help` Lists all available commands
`/lunaris dev` Toggles the developer bar
```

Plugin commands follow the same pattern: `/<pluginname> <command>`

Mods can also be installed manually by dropping a `.dll` into the `plugins` folder next to `Erenshor.exe`.
I would generally advice against this, however, since a lot of mods aren't on the Vault you probably don't have much choice.
If you haven't run Lunaris before you can either create the folder manually or run the game once.

**Keep in mind, while unloading mods while the game is running is a main feature, I cannot guarantee legacy (BepInEx) mods to 100% support this and your game might crash! Or some might not work at all!**

Lunaris checks if there is an update available for it everytime you start the game and automatically installs it.

## For Mod Developers
You can still use BepInEx modding with Lunaris, however switching to the Lunaris API is recommended for full compatibility and speed.
Currently Lunaris does not support load ordering so keep this in mind, it is however planned for a future release.

Lunaris also comes with a built-in console(WIP) and unity explorer, use the dev bar to access these.

Please read the [API Documentation](https://mizukibelhi.github.io/Lunaris-Docs/) to learn about specific implementation details.

If you want Lunaris to associate your manually installed mod with the Vault, add the following attribute with your mods Vault ID:
```cs
[assembly: System.Reflection.AssemblyMetadata("LunarisPluginId", "vaultID")]
```

## Included Libraries
Lunaris ships with the following, you don't need to bundle them yourself:
```
Mono.Cecil
MonoMod
Newtonsoft.Json
ImGui.NET
dear ImGui
0Harmony
BepInEx (modified, for wrapping only)
```

You can download all libraries except BepInEx [here](https://github.com/MizukiBelhi/Lunaris/releases/download/Libs/LunarisLibs.zip).

## Getting Started
Reference `Lunaris.dll` in your project. Your plugin class needs to extend `LunarisPlugin` and requires the `[LunarisPlugin]` attribute, similar to BepInEx.
```cs
[LunarisPlugin("ModName", "SemVerVersion", "AuthorName", "ShortDescription")]
[LunarisPermission(LunarisPermission.None)]
public class MyMod : LunarisPlugin
{
    void Awake()
    {
        Logging.Log("Hello from MyMod!");
    }
}
```

For testing, you can simply replace your dll during runtime inside the plugins folder, unless you have disabled it, Lunaris will automatically
reload your mod.

## Permissions
The permission attribute is not required, and currently doesn't serve a real purpose besides letting the user know that you did not add it.

## Commands
```cs
[LunarisCommand("test", "A test command.")]
public static void Command_Test(string name)
{
    // /myplugin test somestring
}
```
When adding a command, Lunaris takes your sanitized plugin name (set through the `[LunarisPlugin]` attribute) and automatically registers commands in-game. You no longer need to manually hook `CheckCommands`.

## Config
The preferred way to use configs with Lunaris is through `Config.Register<T>()`. Lunaris also implements a more low-level way, please see the docs for more details.

## IPC (Aura)
Lunaris includes an IPC system for communication between mods without directly referencing each other.
However, this *does* require mods to actually expose endpoints.

## Cleanup
Since Lunaris allows unloading mods during runtime you need to ensure to clean up *everything* during OnDestroy().
Unity does not allow assemblies to live outside of its own context, which makes it incredibly hard to remove your work from memory.
**Not cleaning up after yourself means you are directly causing memory leaks, ESPECIALLY when using ImGui.**
