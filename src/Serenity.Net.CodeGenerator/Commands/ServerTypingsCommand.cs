﻿using Newtonsoft.Json.Linq;
using Serenity.CodeGeneration;

namespace Serenity.CodeGenerator
{
    public class ServerTypingsCommand : BaseFileSystemCommand
    {
        public ServerTypingsCommand(IGeneratorFileSystem fileSystem) 
            : base(fileSystem)
        {
        }

        public void Run(string csproj, List<ExternalType> tsTypes)
        {
            var projectDir = fileSystem.GetDirectoryName(csproj);
            var config = GeneratorConfig.LoadFromFile(fileSystem, fileSystem.Combine(projectDir, "sergen.json"));

            string[] assemblyFiles = null;

            if (config.ServerTypings == null ||
                config.ServerTypings.Assemblies.IsEmptyOrNull())
            {
                var targetFramework = ProjectFileHelper.ExtractTargetFrameworkFrom(fileSystem, csproj);

                if (string.IsNullOrEmpty(targetFramework))
                {
                    Console.Error.WriteLine("Couldn't read TargetFramework from " +
                        "project file for server typings generation!");
                    Environment.Exit(1);
                }

                string outputName = ProjectFileHelper.ExtractAssemblyNameFrom(fileSystem, csproj)
                    ?? fileSystem.ChangeExtension(fileSystem.GetFileName(csproj), null);

                var outputExtension = ".dll";
                if (targetFramework.StartsWith("net", StringComparison.OrdinalIgnoreCase) &&
                    !targetFramework.StartsWith("netcoreapp", StringComparison.Ordinal) &&
                    targetFramework.IndexOf('.', StringComparison.Ordinal) < 0)
                    outputExtension = ".exe";

                var outputPath1 = fileSystem.Combine(fileSystem.GetDirectoryName(csproj),
                    PathHelper.ToPath("bin/Debug/" + targetFramework + "/" + outputName + outputExtension));
                var outputPath2 = fileSystem.Combine(fileSystem.GetDirectoryName(csproj), 
                    PathHelper.ToPath("bin/Release/" + targetFramework + "/" + outputName + outputExtension));

                if (fileSystem.FileExists(outputPath1))
                {
                    if (fileSystem.FileExists(outputPath2) &&
                        fileSystem.GetLastWriteTime(outputPath1) < fileSystem.GetLastWriteTime(outputPath2))
                        assemblyFiles = new[] { outputPath2 };
                    else
                        assemblyFiles = new[] { outputPath1 };
                }
                else if (fileSystem.FileExists(outputPath2))
                    assemblyFiles = new[] { outputPath2 };
                else
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture,
                        "Couldn't find output file for server typings generation at {0}!" + Environment.NewLine + 
                        "Make sure project is built successfully before running Sergen", outputPath1));
                    Environment.Exit(1);
                }
            }

            if (assemblyFiles == null)
            {
                if (config.ServerTypings == null)
                {
                    Console.Error.WriteLine("ServerTypings is not configured in sergen.json file!");
                    Environment.Exit(1);
                }

                if (config.ServerTypings.Assemblies.IsEmptyOrNull())
                {
                    Console.Error.WriteLine("ServerTypings has no assemblies configured in sergen.json file!");
                    Environment.Exit(1);
                }

                assemblyFiles = config.ServerTypings.Assemblies;
                for (var i = 0; i < assemblyFiles.Length; i++)
                {
                    var assemblyFile1 = PathHelper.ToUrl(fileSystem.GetFullPath(PathHelper.ToPath(assemblyFiles[i])));
                    var binDebugIdx = assemblyFile1.IndexOf("/bin/Debug/", StringComparison.OrdinalIgnoreCase);
                    string assemblyFile2 = assemblyFile1;
                    if (binDebugIdx >= 0)
                        assemblyFile2 = string.Concat(assemblyFile1.AsSpan(0, binDebugIdx), "/bin/Release/", assemblyFile1[(binDebugIdx + "/bin/Release".Length)..]);

                    assemblyFiles[i] = assemblyFile1;

                    if (fileSystem.FileExists(assemblyFile1))
                    {
                        if (fileSystem.FileExists(assemblyFile2) &&
                            fileSystem.GetLastWriteTime(assemblyFile1) < fileSystem.GetLastWriteTime(assemblyFile2))
                            assemblyFiles[i] = assemblyFile2;
                    }
                    else if (fileSystem.FileExists(assemblyFile2))
                        assemblyFiles[i] = assemblyFile2;
                    else
                    {
                        Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, string.Format(CultureInfo.CurrentCulture, 
                            "Assembly file '{0}' specified in sergen.json is not found! " +
                            "This might happen when project is not successfully built or file name doesn't match the output DLL." +
                            "Please check path in sergen.json and try again.", assemblyFile1)));
                        Environment.Exit(1);
                    }
                }
            }

            if (config.RootNamespace.IsEmptyOrNull())
                config.RootNamespace = config.GetRootNamespaceFor(fileSystem, csproj);

            var generator = new ServerTypingsGenerator(fileSystem, assemblyFiles.ToArray())
            {
                LocalTexts = config.ServerTypings != null && config.ServerTypings.LocalTexts
            };

            var appSettings = fileSystem.Combine(projectDir, "appsettings.json");
            if (generator.LocalTexts && fileSystem.FileExists(appSettings))
            {
                try
                {
                    var obj = JObject.Parse(fileSystem.ReadAllText(appSettings));
                    if ((obj["AppSettings"] as JObject)?["LocalTextPackages"] is JObject packages)
                    {
                        foreach (var p in packages.PropertyValues())
                            foreach (var x in p.Values<string>())
                                generator.LocalTextFilters.Add(x);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error occurred while parsing appsettings.json!" + Environment.NewLine + 
                        ex.ToString());
                }
            }

            var outDir = fileSystem.Combine(projectDir, PathHelper.ToPath((config.ServerTypings?.OutDir.TrimToNull() ?? "Imports/ServerTypings")));

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Transforming ServerTypings at: ");
            Console.ResetColor();
            Console.WriteLine(outDir);

            generator.RootNamespaces.Add(config.RootNamespace);

            foreach (var type in tsTypes)
                generator.AddTSType(type);

            var codeByFilename = generator.Run();
            MultipleOutputHelper.WriteFiles(fileSystem, outDir, codeByFilename, "*.ts");
        }
    }
}