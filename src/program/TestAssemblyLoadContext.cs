using System.Reflection;
using System.Runtime.Loader;

class TestAssemblyLoadContext : AssemblyLoadContext
{
    public TestAssemblyLoadContext() : base(isCollectible: true)
    {
    }

    protected override Assembly? Load(AssemblyName name)
    {
        /* Per https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
         * The Load method returns null. That means that all the dependency assemblies
         * are loaded into the default context, and the new context contains only the
         * assemblies explicitly loaded into it.
         */
        return null;
    }
}