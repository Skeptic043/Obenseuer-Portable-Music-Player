using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PortableMusicPlayer.Tests
{
    // Runs every public static void method of every [TestSuite] class and reports each failure.
    // The first argument is the installed game folder, for tests that read the game's own files.
    internal static class Program
    {
        internal static string GamePath { get; private set; }
        internal static string BepInEx6CorePath { get; private set; }

        private static int Main(string[] args)
        {
            GamePath = args.Length > 0 ? args[0] : null;
            BepInEx6CorePath = args.Length > 1 ? args[1] : null;
            var failures = new List<string>();
            int passed = 0;
            foreach (Type suite in typeof(Program).Assembly.GetTypes().Where(t => t.GetCustomAttribute<TestSuiteAttribute>() != null).OrderBy(t => t.Name))
            foreach (MethodInfo test in suite.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.GetParameters().Length == 0).OrderBy(m => m.MetadataToken))
            {
                try { test.Invoke(null, null); passed++; }
                catch (TargetInvocationException error) { failures.Add(suite.Name + "." + test.Name + ": " + error.InnerException.Message); }
            }
            foreach (string failure in failures) Console.Error.WriteLine("FAIL " + failure);
            Console.WriteLine(passed + " passed, " + failures.Count + " failed.");
            return failures.Count == 0 ? 0 : 1;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class TestSuiteAttribute : Attribute { }

    internal static class Assert
    {
        internal static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        internal static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(what + ": expected " + expected + ", got " + actual);
        }

        internal static void Near(double expected, double actual, double tolerance, string what)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception(what + ": expected " + expected + " ± " + tolerance + ", got " + actual);
        }
    }
}
