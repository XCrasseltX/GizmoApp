using System.Reflection;
using System.Text.Json;

using GizmoApp.Models;

namespace GizmoApp.Service
{
    public static class ConfigService
    {
        public static async Task<AppConfig> LoadConfigAsync()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "GizmoApp.Resources.Raw.config.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException("Could not find embedded resource: " + resourceName);
            }
            using var reader = new StreamReader(stream);

            string json = await reader.ReadToEndAsync();
            var config = JsonSerializer.Deserialize<AppConfig>(json);

            return config!;
        }
    }
}
