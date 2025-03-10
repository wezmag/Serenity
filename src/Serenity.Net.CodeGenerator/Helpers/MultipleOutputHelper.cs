﻿namespace Serenity.CodeGenerator
{
    public class MultipleOutputHelper
    {
        private static readonly Encoding utf8 = new UTF8Encoding(true);

        public static void WriteFiles(IGeneratorFileSystem fileSystem,
            string outDir, SortedDictionary<string, string> codeByFilename, params string[] deleteExtraPattern)
        {
            if (fileSystem is null)
                throw new ArgumentNullException(nameof(fileSystem));

            fileSystem.CreateDirectory(outDir);

            var generated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in codeByFilename)
            {
                generated.Add(pair.Key);

                var outFile = fileSystem.Combine(outDir, pair.Key);
                bool exists = fileSystem.FileExists(outFile);
                if (exists)
                {
                    var content = fileSystem.ReadAllText(outFile, utf8);
                    if (content.Trim().Replace("\r", "", StringComparison.Ordinal) ==
                        pair.Value.Trim().Replace("\r", "", StringComparison.Ordinal))
                        continue;
                }

#if !ISSOURCEGENERATOR
                Console.ForegroundColor = exists ? ConsoleColor.Magenta : ConsoleColor.Green;
                Console.Write(exists ? "Overwriting: " : "New File: ");
                Console.ResetColor();
                Console.WriteLine(fileSystem.GetFileName(outFile));
#endif

                fileSystem.WriteAllText(outFile, pair.Value, utf8);
            }

            if (deleteExtraPattern.Length == 0)
                return;

            var filesToDelete = deleteExtraPattern.SelectMany(
                    x => fileSystem.GetFiles(outDir, x))
                .Distinct();

            foreach (var file in filesToDelete)
                if (!generated.Contains(fileSystem.GetFileName(file)))
                {
#if !ISSOURCEGENERATOR
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("Deleting: ");
                    Console.ResetColor();
                    Console.WriteLine(fileSystem.GetFileName(file));
#endif
                    fileSystem.DeleteFile(file);
                }
        }
    }
}