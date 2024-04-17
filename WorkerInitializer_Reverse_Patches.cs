using HarmonyLib;
using FrooxEngine;

namespace CherryPick;

[HarmonyPatch(typeof(WorkerInitializer))]
public static class WorkerInitializer_Reverse_Patches
{

    [HarmonyReversePatch]
    [HarmonyPatch("GetInitInfo")]
    public static WorkerInitInfo GetInitInfo(this Type workerType) => throw new NotImplementedException("Harmony stub");
}