using Eremite.Buildings;
using Eremite.Controller.Buildings;
using HarmonyLib;
using StockAlert.UI.World;

namespace StockAlert.Infrastructure.Hooks
{
    [HarmonyPatch(typeof(HubsController), "UpdateHubsTier")]
    internal static class HearthLevelDownAlertPatch
    {
        private static void Prefix(Hearth hearth, ref int __state)
        {
            __state = hearth?.state?.hubIndex ?? -1;
        }

        private static void Postfix(Hearth hearth, int __state)
        {
            var current = hearth?.state?.hubIndex ?? -1;
            if (__state <= current || hearth?.IsRemoved?.Value == true)
            {
                return;
            }

            HearthLevelDownAlert.Fire(hearth, __state, current);
        }
    }
}
