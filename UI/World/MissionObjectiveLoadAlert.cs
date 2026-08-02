using Eremite.Model;
using Eremite.Services.Monitors;
using StockAlert.Config;
using StockAlert.Game;

namespace StockAlert.UI.World
{
    internal static class MissionObjectiveLoadAlert
    {
        private static News _activeNews;

        public static void Fire()
        {
            if (!ConfigManager.MissionObjectiveLoadAlert || !GameAPI.IsGameActive())
            {
                Clear();
                return;
            }

            Clear();
            _activeNews = GameAPI.PublishNews(
                "Save loaded: check mission objective.",
                "Review your settlement objective before unpausing or making changes.",
                AlertSeverity.Info
            );
        }

        public static void Clear()
        {
            if (_activeNews == null)
            {
                return;
            }

            GameAPI.RemoveNews(_activeNews);
            _activeNews = null;
        }
    }
}
