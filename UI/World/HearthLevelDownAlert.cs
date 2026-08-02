using System.Collections.Generic;
using System.Linq;
using Eremite.Buildings;
using Eremite.Model;
using Eremite.Services.Monitors;
using StockAlert.Config;
using StockAlert.Game;

namespace StockAlert.UI.World
{
    internal static class HearthLevelDownAlert
    {
        private static readonly List<News> ActiveAlerts = new List<News>();

        public static void Fire(Hearth hearth, int previousLevel, int currentLevel)
        {
            if (!ConfigManager.HearthLevelDownAlert || !GameAPI.IsGameActive() || hearth == null)
            {
                return;
            }

            var displayName = GetDisplayName(hearth);
            var previousText = FormatLevel(previousLevel);
            var currentText = FormatLevel(currentLevel);
            var content = $"{displayName} decreased from {previousText} to {currentText}.";
            var description = "Check housing, decorations, and service buildings inside the Hearth's range.";
            var news = GameAPI.PublishNews(content, description, AlertSeverity.Warning);

            if (news != null)
            {
                ActiveAlerts.Add(news);
            }
        }

        public static void Refresh()
        {
            if (!ConfigManager.HearthLevelDownAlert || !GameAPI.IsGameActive())
            {
                Clear();
                return;
            }
        }

        public static void Clear()
        {
            foreach (var alert in ActiveAlerts.ToList())
            {
                GameAPI.RemoveNews(alert);
            }

            ActiveAlerts.Clear();
        }

        private static string GetDisplayName(Hearth hearth)
        {
            var text = hearth.BuildingModel?.displayName?.Text;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return hearth.IsMainHearth() ? "Ancient Hearth" : "Hearth";
        }

        private static string FormatLevel(int levelIndex)
        {
            if (levelIndex < 0)
            {
                return "level 0";
            }

            return "level " + (levelIndex + 1);
        }
    }
}
