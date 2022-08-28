# .NET Core Assembly Unloading

```text
>>>> Chart for Sin(d/3)
  1.00 ┤   ╭──╮               ╭─╮                ╭─╮               ╭──╮               ╭──╮               ╭─
  0.80 ┤  ╭╯  ╰╮             ╭╯ ╰╮              ╭╯ ╰╮             ╭╯  ╰╮             ╭╯  ╰╮             ╭╯
  0.60 ┤ ╭╯    │            ╭╯   ╰╮            ╭╯   ╰╮            │    ╰╮           ╭╯    ╰╮           ╭╯
  0.40 ┤╭╯     ╰╮          ╭╯     ╰╮          ╭╯     ╰╮          ╭╯     ╰╮          │      │           │
  0.20 ┤│       ╰╮         │       │         ╭╯       │         ╭╯       │         ╭╯      ╰╮         ╭╯
 -0.00 ┼╯        │        ╭╯       ╰╮        │        ╰╮        │        ╰╮        │        ╰╮       ╭╯
 -0.20 ┤         ╰╮      ╭╯         ╰╮      ╭╯         ╰╮      ╭╯         │       ╭╯         │       │
 -0.40 ┤          │      │           │      │           │     ╭╯          ╰╮     ╭╯          ╰╮     ╭╯
 -0.60 ┤          ╰╮    ╭╯           ╰╮    ╭╯           ╰╮    │            ╰╮    │            ╰╮   ╭╯
 -0.80 ┤           ╰╮  ╭╯             ╰╮  ╭╯             ╰╮  ╭╯             ╰╮  ╭╯             ╰╮ ╭╯
 -1.00 ┤
```

## Links

- [MS Doc: AssemblyLoadContext Class](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext)
- [MS Doc: How to use and debug assembly unloadability in .NET Core](https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability)
- [MS Doc: Understanding System.Runtime.Loader.AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Exploring the new Assembly unloading feature in .NET Core 3.0 by building a simple plugin system running on ASP.NET Core Blazor](https://stevenknox.net/exploring-assembly-unloading-in-net-core-3-0-by-building-a-simple-plugin-architecture/) by Steve Knox
