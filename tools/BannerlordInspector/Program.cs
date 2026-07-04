using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BannerlordInspector
{
    internal static class Program
    {
        private static readonly BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static int Main(string[] args)
        {
            var options = Options.Parse(args);
            if (string.IsNullOrWhiteSpace(options.TypeName))
            {
                Console.Error.WriteLine("Usage: BannerlordInspector --type <Full.Type.Name> [--member <name>] [--assembly <dll-or-name>] [--list]");
                return 2;
            }
            string typeName = options.TypeName!;

            string gameBin = Environment.GetEnvironmentVariable("BANNERLORD_GAME_BIN")
                ?? @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client";

            if (!Directory.Exists(gameBin))
            {
                Console.Error.WriteLine($"Bannerlord bin directory does not exist: {gameBin}");
                return 2;
            }

            AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) => ResolveAssembly(gameBin, eventArgs.Name);

            var assemblies = LoadAssemblies(gameBin, options.AssemblyName).ToList();
            var type = FindType(assemblies, typeName);
            if (type == null)
            {
                Console.Error.WriteLine($"Type not found: {typeName}");
                return 1;
            }

            PrintType(type, options);
            return 0;
        }

        private static Assembly? ResolveAssembly(string gameBin, string assemblyName)
        {
            string simpleName = new AssemblyName(assemblyName).Name + ".dll";
            string path = Path.Combine(gameBin, simpleName);
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }

        private static IEnumerable<Assembly> LoadAssemblies(string gameBin, string? requestedAssembly)
        {
            var dlls = string.IsNullOrWhiteSpace(requestedAssembly)
                ? Directory.GetFiles(gameBin, "TaleWorlds*.dll").Concat(Directory.GetFiles(gameBin, "Newtonsoft.Json.dll"))
                : new[] { ResolveAssemblyPath(gameBin, requestedAssembly!) };

            foreach (string dll in dlls.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(dll);
                }
                catch
                {
                    continue;
                }

                yield return assembly;
            }
        }

        private static string ResolveAssemblyPath(string gameBin, string requestedAssembly)
        {
            if (File.Exists(requestedAssembly))
                return requestedAssembly;

            string fileName = requestedAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? requestedAssembly
                : requestedAssembly + ".dll";

            return Path.Combine(gameBin, fileName);
        }

        private static Type? FindType(IEnumerable<Assembly> assemblies, string typeName)
        {
            foreach (var assembly in assemblies)
            {
                Type? type = assembly.GetType(typeName, false, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static void PrintType(Type type, Options options)
        {
            Console.WriteLine($"TYPE {type.FullName}");
            Console.WriteLine($"ASSEMBLY {type.Assembly.GetName().Name}");

            if (type.IsEnum)
            {
                foreach (var value in Enum.GetValues(type))
                    Console.WriteLine($"  ENUM {(int)value} = {value}");
                return;
            }

            var members = GetMembers(type)
                .Where(member => options.List || Matches(member, options.MemberName))
                .OrderBy(member => member.Kind)
                .ThenBy(member => member.Name)
                .ToList();

            foreach (var member in members)
                Console.WriteLine($"  {member.Kind} {member.Signature}");

            if (members.Count == 0 && !string.IsNullOrWhiteSpace(options.MemberName))
                Console.WriteLine($"  No declared members matched '{options.MemberName}'.");
        }

        private static IEnumerable<MemberView> GetMembers(Type type)
        {
            foreach (var constructor in type.GetConstructors(MemberFlags))
                yield return new MemberView(".ctor", "CTOR", constructor.ToString());

            foreach (var method in type.GetMethods(MemberFlags))
                yield return new MemberView(method.Name, "METHOD", method.ToString());

            foreach (var property in type.GetProperties(MemberFlags))
                yield return new MemberView(property.Name, "PROPERTY", $"{property.PropertyType.FullName} {property.Name}");

            foreach (var field in type.GetFields(MemberFlags))
                yield return new MemberView(field.Name, "FIELD", $"{field.FieldType.FullName} {field.Name}");

            foreach (var eventInfo in type.GetEvents(MemberFlags))
                yield return new MemberView(eventInfo.Name, "EVENT", $"{eventInfo.EventHandlerType?.FullName} {eventInfo.Name}");
        }

        private static bool Matches(MemberView member, string? memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
                return true;

            return member.Name.IndexOf(memberName!, StringComparison.OrdinalIgnoreCase) >= 0
                || member.Signature.IndexOf(memberName!, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class MemberView
        {
            public MemberView(string name, string kind, string? signature)
            {
                Name = name;
                Kind = kind;
                Signature = signature ?? name;
            }

            public string Name { get; }
            public string Kind { get; }
            public string Signature { get; }
        }

        private sealed class Options
        {
            public string? TypeName { get; private set; }
            public string? MemberName { get; private set; }
            public string? AssemblyName { get; private set; }
            public bool List { get; private set; }

            public static Options Parse(string[] args)
            {
                var options = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i];
                    if (arg == "--type" && i + 1 < args.Length)
                        options.TypeName = args[++i];
                    else if (arg == "--member" && i + 1 < args.Length)
                        options.MemberName = args[++i];
                    else if (arg == "--assembly" && i + 1 < args.Length)
                        options.AssemblyName = args[++i];
                    else if (arg == "--list")
                        options.List = true;
                }

                return options;
            }
        }
    }
}
