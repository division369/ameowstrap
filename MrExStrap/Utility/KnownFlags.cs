using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExploitStrap.Utility
{
    // A best-effort database of real Roblox FastFlag names, pulled live from Roblox's own client
    // settings endpoint. Used to (a) mark whether a flag in the editor is a known/real flag and (b)
    // back the "browse flags" search dialog. Loaded once per session, cached.
    public static class KnownFlags
    {
        private const string LOG_IDENT = "KnownFlags";

        private static readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

        public static bool Loaded { get; private set; }

        public static IReadOnlyCollection<string> Names => _names;

        public static bool IsKnown(string? name) => !string.IsNullOrEmpty(name) && _names.Contains(name);

        public static async Task LoadAsync()
        {
            if (Loaded)
                return;

            try
            {
                using var resp = await App.HttpClient.GetAsync(
                    "https://clientsettings.roblox.com/v2/settings/application/PCDesktopClient");

                if (!resp.IsSuccessStatusCode)
                    return;

                string json = await resp.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("applicationSettings", out var settings)
                    && settings.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in settings.EnumerateObject())
                        _names.Add(prop.Name);
                }

                Loaded = _names.Count > 0;
                App.Logger.WriteLine(LOG_IDENT, $"Loaded {_names.Count} known flags");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Load", ex);
            }
        }

        // Filtered, sorted, capped list for the browse dialog.
        public static List<string> Search(string query, int limit = 500)
        {
            IEnumerable<string> source = _names;

            if (!string.IsNullOrWhiteSpace(query))
                source = source.Where(n => n.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

            return source.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Take(limit).ToList();
        }
    }
}
