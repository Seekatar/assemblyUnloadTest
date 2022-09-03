# .NET Core Assembly Unloading

This is a little PoC for unloading assemblies in .NET Core. I had done this in the past with .NET Framework, but the APIs are different for .NET Core.

This console app does a few things by pressing a single key.

Key|Action
-|-
a|Load `libA` library built separately
b|Load `libB` library built separately
c|Call `Message()` on all loaded assemblies
d|Dump info about the assemblies
g|Call garbage collector
p|Call `Value()` with 1-50 and plot results for all loaded assemblies
r|Enter an equation for dynamically built and loaded assembly
1|Build and load 1 assembly
2|Build and load 1000 assemblies
u|Unload all assemblies
q|Quit

For `r` you are prompted for a simple math expression. `System.Math` methods are available without a prefix (e.g. `Sin(d)`). The expression is then used to pass to  Rosyln to build and load the assembly on-the-fly. As a fun little exercise, pressing `p` plots them using  [Ascii Chart C#](https://github.com/NathanBaulch/asciichart-sharp).

```text
>>>> Chart for Sin(d/4)
  1.00 ┤    ╭───╮                    ╭───╮
  0.80 ┤   ╭╯   ╰╮                  ╭╯   ╰╮
  0.60 ┤  ╭╯     ╰╮                ╭╯     ╰╮
  0.40 ┤ ╭╯       ╰╮              ╭╯       ╰╮
  0.20 ┤╭╯         ╰╮            ╭╯         ╰╮
 -0.00 ┼╯           │           ╭╯           ╰╮
 -0.20 ┤            ╰╮         ╭╯             │
 -0.40 ┤             ╰╮        │              ╰╮        ╭
 -0.60 ┤              ╰╮      ╭╯               ╰╮      ╭╯
 -0.80 ┤               ╰─╮  ╭─╯                 ╰─╮  ╭─╯
 -1.00 ┤                 ╰──╯                     ╰──╯
>>>> Chart for Cos(d/4)
  1.00 ┼─╮                     ╭──╮                     ╭
  0.80 ┤ ╰─╮                 ╭─╯  ╰─╮                  ╭╯
  0.60 ┤   ╰╮               ╭╯      ╰╮                ╭╯
  0.40 ┤    ╰╮              │        ╰╮              ╭╯
  0.20 ┤     │             ╭╯         ╰╮            ╭╯
  0.00 ┤     ╰╮           ╭╯           │           ╭╯
 -0.20 ┤      ╰╮         ╭╯            ╰╮         ╭╯
 -0.40 ┤       ╰╮       ╭╯              ╰╮       ╭╯
 -0.60 ┤        ╰╮     ╭╯                ╰╮     ╭╯
 -0.80 ┤         ╰╮   ╭╯                  ╰╮   ╭╯
 -1.00 ┤          ╰───╯                    ╰───╯
>>>> Chart for d*d
 2401.00 ┤                                               ╭─
 2160.90 ┤                                             ╭─╯
 1920.80 ┤                                          ╭──╯
 1680.70 ┤                                       ╭──╯
 1440.60 ┤                                    ╭──╯
 1200.50 ┤                                ╭───╯
  960.40 ┤                            ╭───╯
  720.30 ┤                        ╭───╯
  480.20 ┤                  ╭─────╯
  240.10 ┤          ╭───────╯
    0.00 ┼──────────╯
```

## Notes

From [here](https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability)

The AssemblyLoadContext-derived class returns null to indicate that it should be loaded into the default context from locations that the runtime uses to load assemblies by default.

After the calling a method in the loaded assembly, you can initiate unloading by either calling the Unload method on the custom AssemblyLoadContext or getting rid of the reference you have to the AssemblyLoadContext

## Design

Load assemblies
  AssemblyManager<T>()
  Load(into named bucket)
  Unload(bucketName)
  Type Array internally
  Create with Activator on demand <T>Create(name, args)
  
  Register with DI and then they can inject, or call us on demand << won't that create hard dep in the ServiceLocator? So can't unload

## Links

- [MS Doc: AssemblyLoadContext Class](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)
- [MS Doc: How to use and debug assembly unloadability in .NET Core](https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability)
- [MS Doc: Understanding System.Runtime.Loader.AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [MS Doc: How to use and debug assembly unloadability in .NET Core](https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability)
- [Exploring the new Assembly unloading feature in .NET Core 3.0 by building a simple plugin system running on ASP.NET Core Blazor](https://stevenknox.net/exploring-assembly-unloading-in-net-core-3-0-by-building-a-simple-plugin-architecture/) by Steve Knox
- [Ascii Chart C#](https://github.com/NathanBaulch/asciichart-sharp)