using AssemblyContextTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using static System.Console;
using System.Runtime.CompilerServices;

namespace program
{
    internal class TestCollection
    {
        private readonly AssemblyManager _manager;
        private List<ITest>? _tests = new();
        WeakReference hostAlcWeakRef2;
        
        public TestCollection(AssemblyManager manager)
        {
            _manager = manager;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Load(string path)
        {
            var t = AssemblyManager.Get(path.Replace("program", "libB"), out hostAlcWeakRef2);
            WriteLine(t.Message("HI from TestCollection!!!!!"));
            _tests.Add(t);
            //t = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Call()
        {
            foreach (var test in _tests)
            {
                WriteLine(test.Message("Hi from Call()"));
            }
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Unload()
        {
            _tests = new();
            
            var alc = hostAlcWeakRef2.Target as AssemblyLoadContext;
            alc!.Unload();
            alc = null;

            // Poll and run GC until the AssemblyLoadContext is unloaded.
            // You don't need to do that unless you want to know when the context
            // got unloaded. You can just leave it to the regular GC.
            for (int i = 0; hostAlcWeakRef2.IsAlive && (i < 100); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(4);
            }

            Console.WriteLine($"Unload success: {!hostAlcWeakRef2.IsAlive}");
        }
    }
}
