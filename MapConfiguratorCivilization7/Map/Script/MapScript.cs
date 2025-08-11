using MapConfiguratorCivilization7.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7
{

    public class MapScript
    {
        public MethodInfo entryPoint = null;
        public Type entryType = null;
        public MapScriptSettings settings;
        public string name, scriptPath;
        public bool isValid = false;

        public Task<string> InitializationTask { get; private set; }
        public Task ScriptTask { get; private set; }
        private CancellationTokenSource scriptCts, throttleDelayCts = new CancellationTokenSource();
        private TimeSpan throttleInterval = TimeSpan.FromMilliseconds(200);
        private DateTime lastExecution = DateTime.MinValue;

        public MapScriptData data;

        public MapScript(string name, string scriptPath)
        {
            this.name = name;
            this.scriptPath = scriptPath;
            data = new MapScriptData(MapData.MAX_SIZE_X, MapData.MAX_SIZE_Y, App.map.seed);
            settings = new MapScriptSettings(this);
            StartInitialize();
        }

        public bool CreateMap()
        {
            if (!isValid)
                return false;

            data.width = App.map.mapSize.X;
            data.height = App.map.mapSize.Y;
            data.seed = App.map.seed;
            data.playerHome = App.map.selectedPlayerCount.X;
            data.playerDistant = App.map.selectedPlayerCount.Y;

            scriptCts = new CancellationTokenSource();
            var scriptInstance = Activator.CreateInstance(entryType, [this, scriptCts.Token]);
            ScriptTask = Task.Run(() => 
            { 
                try
                {
                    Array.Clear(data.debug);
                    entryPoint.Invoke(scriptInstance, null);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is OperationCanceledException) { }
            }, scriptCts.Token);
            return true;
        }

        public void CancelScript()
        {
            scriptCts?.Cancel();
            scriptCts?.Dispose();
            scriptCts = null;
        }

        public void RenderSettings()
        {
            bool anyChange = settings.RenderImGui();
            if (!anyChange)
                return;

            ApplyChanges();
        }

        public void ApplyChanges(bool force = false)
        {
            var now = DateTime.UtcNow;

            // Run if forced or interval has elapsed
            if (force || (now - lastExecution) >= throttleInterval)
            {
                lastExecution = now;
                CancelScript();
                CreateMap();
                return;
            }

            // Cancel any scheduled delayed execution
            throttleDelayCts.Cancel();
            throttleDelayCts.Dispose();
            throttleDelayCts = new CancellationTokenSource();
            var delayToken = throttleDelayCts.Token;

            // Schedule update to run after the remaining interval
            var delay = throttleInterval - (now - lastExecution);

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, delayToken);
                    if (delayToken.IsCancellationRequested)
                        return;

                    lastExecution = DateTime.UtcNow;
                    CancelScript();
                    CreateMap();
                }
                catch (TaskCanceledException) { }
            });
        }

        // Call from script to sync script data for rendering
        public void UpdateMapCallback()
        {
            App.map.mapData.Set(data);
        }

        public void StartInitialize()
        {
            isValid = false;
            entryPoint = null;
            InitializationTask = Task.Run(() => Initialize());
        }

        private string Initialize()
        {
            string code = File.ReadAllText(Path.Combine(scriptPath, "script.cs"));

            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            string assemblyName = Path.GetRandomFileName();

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(typeof(MapScript).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Vector2).Assembly.Location),
            };

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: options
            );

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                string error = $"Errors building {scriptPath}:";
                foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    error += "\n  " + diagnostic.ToString();
                }
                return error;
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var type = assembly.GetType("Script");
            if (type == null)
                return "Could not find class \"Script\"";
            entryType = type;

            var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
                return "Could not find method \"Run\"";
            entryPoint = method;


            var settingsType = assembly.GetType("ScriptSettings");
            if (settingsType == null)
                return "Error reading script setting data: class ScriptSettings not found!";

            foreach (var config in Directory.GetFiles(scriptPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (config.EndsWith("ConfigForGame.json"))
                    continue;

                settings.configs.Add(Path.GetFileNameWithoutExtension(config));
            }

            settings.SetDefaultInstance(Activator.CreateInstance(settingsType));

            isValid = true;

            CreateMap();
            return "";
        }
    }
}
