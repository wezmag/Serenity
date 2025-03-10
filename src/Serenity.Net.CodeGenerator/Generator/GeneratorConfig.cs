﻿using Newtonsoft.Json.Linq;

namespace Serenity.CodeGenerator
{
    public class GeneratorConfig
    {
        public string RootNamespace { get; set; }
        public ServerTypingsConfig ServerTypings { get; set; }
        
        public ClientTypesConfig ClientTypes { get; set; }
        public bool ShouldSerializeClientTypes() => ClientTypes != null &&
            !string.IsNullOrEmpty(ClientTypes.OutDir);

        public MVCConfig MVC { get; set; }
        public bool ShouldSerializeMVC() => MVC != null &&
            (!string.IsNullOrEmpty(MVC.OutDir) ||
             MVC.UseRootNamespace != null ||
             MVC.SearchViewPaths?.Any() == true ||
             MVC.StripViewPaths?.Any() == true);

        public RestoreConfig Restore { get; set; }
        public bool ShouldSerializeRestore() => Restore != null &&
            (!Restore.Include.IsEmptyOrNull() ||
             !Restore.Exclude.IsEmptyOrNull());

        public List<Connection> Connections { get; set; }
        public bool ShouldSerializeConnections() => Connections != null && Connections.Count > 0;

        public string KDiff3Path { get; set; }
        public bool ShouldSerializeKDiff3Path() => !string.IsNullOrEmpty(KDiff3Path);

        public string TSCPath { get; set; }
        public bool ShouldSerializeTSCPath() => !string.IsNullOrEmpty(TSCPath);

        public List<BaseRowClass> BaseRowClasses { get; set; }
        public bool ShouldSerializeBaseRowClasses() =>
            BaseRowClasses != null && BaseRowClasses.Any();

        public List<string> RemoveForeignFields { get; set; }
        public bool ShouldSerializeRemoveForeignFields() =>
            RemoveForeignFields != null && RemoveForeignFields.Any();

        public bool ShouldSerializeCustomTemplates() =>
            !string.IsNullOrEmpty(CustomTemplates);
        public string CustomTemplates { get; set; }

        public Dictionary<string, string> CustomGenerate { get; set; }
        public bool ShouldSerializeCustomGenerate() =>
            CustomGenerate != null && CustomGenerate.Any();

        public Dictionary<string, object> CustomSettings { get; set; }
        public bool ShouldSerializeCustomSettings() =>
            CustomSettings != null && CustomSettings.Any();

        public string[] AppSettingFiles { get; set; }
        public bool ShouldSerializeAppSettingFiles() =>
            AppSettingFiles != null && AppSettingFiles.Any();

        [JsonIgnore]
        public bool GenerateRow { get; set; }
        [JsonIgnore]
        public bool GenerateService { get; set; }
        [JsonIgnore]
        public bool GenerateUI { get; set; }
        [JsonIgnore]
        public bool GenerateCustom { get; set; }

        public UpgradeInformation UpgradeInfo { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtensionData { get; set; }

        public GeneratorConfig()
        {
            Connections = new List<Connection>();
            BaseRowClasses = new List<BaseRowClass>();
            CustomSettings = new Dictionary<string, object>();
            CustomGenerate = new Dictionary<string, string>();
            GenerateRow = true;
            GenerateService = true;
            GenerateUI = true;
            GenerateCustom = true;
        }

        public string SaveToJson()
        {
            Connections.Sort((x, y) => string.Compare(x.Key, y.Key, StringComparison.OrdinalIgnoreCase));
            foreach (var c in Connections)
                c.Tables.Sort((x, y) => string.Compare(x.Tablename, y.Tablename, StringComparison.OrdinalIgnoreCase));

            return JSON.StringifyIndented(this, 2);
        }

        public string[] GetAppSettingsFiles()
        {
            if (AppSettingFiles != null &&
                AppSettingFiles.Length != 0)
                return AppSettingFiles;

            return new string[]
            {
                "appsettings.json",
                "appsettings.machine.json"
            };
        }

        public string GetRootNamespaceFor(IGeneratorFileSystem fileSystem, string csproj)
        {
            if (fileSystem is null)
                throw new ArgumentNullException(nameof(fileSystem));

            if (!string.IsNullOrEmpty(RootNamespace))
                return RootNamespace;

            string rootNamespace = null;

            if (fileSystem.FileExists(csproj)) {
                 rootNamespace = ProjectFileHelper.ExtractPropertyFrom(fileSystem, csproj,
                    xe => xe.Descendants("RootNamespace").FirstOrDefault()?.Value.TrimToNull());
            }

            if (rootNamespace == null)
                rootNamespace = fileSystem.ChangeExtension(fileSystem.GetFileName(csproj), null);

            if (rootNamespace?.EndsWith(".Web", StringComparison.OrdinalIgnoreCase) == true)
                rootNamespace = rootNamespace[0..^4];

            return rootNamespace;
        }

        public static GeneratorConfig LoadFromFile(IGeneratorFileSystem fileSystem,
            string sergenJson)
        {
            if (fileSystem is null)
                throw new ArgumentNullException(nameof(fileSystem));

            if (!fileSystem.FileExists(sergenJson))
                return LoadFromJson(null);

            return LoadFromJson(fileSystem.ReadAllText(sergenJson));
        }

        public static GeneratorConfig LoadFromJson(string json)
        {
            var config = System.Text.Json.JsonSerializer.Deserialize<GeneratorConfig>(
                json.TrimToNull() ?? "{}", new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            config.Connections ??= new List<Connection>();
            config.RemoveForeignFields ??= new List<string>();
            return config;
        }

        public class Connection
        {
            public string Key { get; set; }
            public string ConnectionString { get; set; }
            public string ProviderName { get; set; }
            public string Dialect { get; set; }
            public List<Table> Tables { get; set; }

            public Connection()
            {
                Tables = new List<Table>();
            }

            public override string ToString()
            {
                return Key + " [" + ConnectionString + "], " + ProviderName;
            }
        }

        public class Table
        {
            public string Tablename { get; set; }
            public string Identifier { get; set; }
            public string Module { get; set; }
            public string PermissionKey { get; set; }
        }
        
        public class BaseRowClass
        {
            public string ClassName { get; set; }
            public List<string> Fields { get; set; }
        }

        public class ServerTypingsConfig
        {
            public string[] Assemblies { get; set; }
            public bool ShouldSerializeAssemblies() => Assemblies != null && Assemblies.Length > 0;

            public string OutDir { get; set; }
            public bool ShouldSerializeOutDir() => !string.IsNullOrEmpty(OutDir);

            public bool LocalTexts { get; set; }
        }

        public class ClientTypesConfig
        {
            public string OutDir { get; set; }
        }

        public class MVCConfig
        {
            public bool? UseRootNamespace { get; set; }
            public string OutDir { get; set; }
            public string[] SearchViewPaths { get; set; }
            public string[] StripViewPaths { get; set; }
        }

        public class RestoreConfig
        {
            public string[] Include { get; set; }
            public string[] Exclude { get; set; }
        }

        public class UpgradeInformation
        {
            public string InitialType { get; set; }
            public string InitialVersion { get; set; }
            public string[] AppliedUpgrades { get; set; }
            [JsonExtensionData]
            public IDictionary<string, JToken> ExtensionData { get; set; }
        }
    }
} 