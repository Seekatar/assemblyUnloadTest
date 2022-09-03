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
        private readonly AssemblyManagerOrig _manager;
        private List<ITest>? _tests = new();
        AssemblyLoadContext _alc;

        public TestCollection(AssemblyManagerOrig manager)
        {
            _manager = manager;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Load(string path)
        {
            WeakReference wr;
            var t = AssemblyManagerOrig.Get(path.Replace("program", "libB"), out wr);
            _alc = wr.Target as AssemblyLoadContext;
            WriteLine(t.Message("HI from TestCollection!!!!!"));
            _tests.Add(t);
            //t = null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void LoadAnother(string path)
        {

            var t = AssemblyManagerOrig.Get(path.Replace("program", "libB"), new WeakReference(_alc));
            if (t is not null)
            {
                WriteLine(t.Message("HI from TestCollection!!!!!"));
                _tests.Add(t);
            } else
            {
                WriteLine("t is null");
            }
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

            var alc = _alc;
            if (alc is null) { Console.Write("alc null in unload"); return; }
            alc.Unload();
            var wr = new WeakReference(alc);
            alc = null;
            _alc = null;

            // Poll and run GC until the AssemblyLoadContext is unloaded.
            // You don't need to do that unless you want to know when the context
            // got unloaded. You can just leave it to the regular GC.
            for (int i = 0; wr.IsAlive && (i < 10); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }

            Console.WriteLine($"Unload success: {!wr.IsAlive}");
        }

        internal ITest? Get(string assyName)
        {
            return _tests.FirstOrDefault();
        }
    }
}
