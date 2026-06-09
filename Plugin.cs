using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace GanaBladePerfFix
{
    // GanaBlade weapon performance fix + key lookup fix.
    [BepInPlugin(GUID, NAME, VERSION)]
    public class PerfFixPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.boxo.ganablade.perffix";
        public const string NAME = "GanaBlade Perf Fix";
        public const string VERSION = "1.2.0";

        private void Awake()
        {
            new Harmony(GUID).PatchAll();
            Logger.LogInfo(NAME + " " + VERSION +
                " loaded: per-frame-cached FindObjectsOfType in PlayerWing.SkillDo + PlayerFire.DiuMission " +
                "(weapons 2 & 3) and O(1) ControlUtil.GetButtonPush input polling");
        }
    }

    internal static class KeyCodeNames
    {
        public static readonly Dictionary<string, KeyCode> Map = Build();

        private static Dictionary<string, KeyCode> Build()
        {
            var d = new Dictionary<string, KeyCode>();
            foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
            {
                d[kc.ToString().ToUpper()] = kc;
            }
            return d;
        }
    }

    // Key Lookup
    [HarmonyPatch(typeof(ControlUtil), "GetButtonPush")]
    internal static class Patch_GetButtonPush
    {
        private static bool Prefix(string key, ref bool __result)
        {
            bool flag = false;

            if (key.IndexOf("LeftRight") > -1 || key.IndexOf("UpDown") > -1)
            {
                string[] array = key.Split('_');
                float num = Input.GetAxisRaw(array[0]);
                if (num > 0f)
                {
                    num = 1f;
                }
                else if (num < 0f)
                {
                    num = -1f;
                }
                if (string.Concat(num) == array[1])
                {
                    flag = true;
                }
            }

            if (!flag)
            {
                KeyCode kc;
                if (KeyCodeNames.Map.TryGetValue(key.ToUpper(), out kc))
                {
                    flag = Input.GetKey(kc);
                }
            }

            __result = flag;
            return false;
        }
    }

    // Weapon fix
    public static class Fire4SuperGunCache
    {
        private static readonly List<Fire4SuperGun> Live = new List<Fire4SuperGun>();
        private static readonly Fire4SuperGun[] Empty = new Fire4SuperGun[0];

        public static void Register(Fire4SuperGun g)
        {
            if (g != null && !Live.Contains(g))
            {
                Live.Add(g);
            }
        }

        public static Fire4SuperGun[] GetAll()
        {
            List<Fire4SuperGun> result = null;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                Fire4SuperGun g = Live[i];
                if (g == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }
                if (g.isActiveAndEnabled)
                {
                    if (result == null)
                    {
                        result = new List<Fire4SuperGun>();
                    }
                    result.Add(g);
                }
            }
            return result == null ? Empty : result.ToArray();
        }
    }

    [HarmonyPatch(typeof(Fire4SuperGun), "OnEnable")]
    internal static class Patch_Fire4SuperGun_OnEnable
    {
        private static void Postfix(Fire4SuperGun __instance)
        {
            Fire4SuperGunCache.Register(__instance);
        }
    }

    public static class PerfCache
    {
        private struct Entry
        {
            public int Frame;
            public UnityEngine.Object[] Array;
        }

        private static readonly Dictionary<Type, Entry> Cache = new Dictionary<Type, Entry>();

        public static T[] Get<T>() where T : UnityEngine.Object
        {
            if (typeof(T) == typeof(Fire4SuperGun))
            {
                return (T[])(object)Fire4SuperGunCache.GetAll();
            }

            int frame = Time.frameCount;
            Entry e;
            if (Cache.TryGetValue(typeof(T), out e) && e.Frame == frame)
            {
                return (T[])e.Array;
            }

            T[] fresh = UnityEngine.Object.FindObjectsOfType<T>();
            e.Frame = frame;
            e.Array = fresh;
            Cache[typeof(T)] = e;
            return fresh;
        }
    }

    [HarmonyPatch]
    internal static class Patch_ReplaceFindObjects
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerWing), "SkillDo");
            yield return AccessTools.Method(typeof(PlayerFire), "DiuMission");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getOpen = AccessTools.Method(typeof(PerfCache), nameof(PerfCache.Get));

            foreach (CodeInstruction ins in instructions)
            {
                if ((ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
                    && ins.operand is MethodInfo mi
                    && mi.Name == "FindObjectsOfType"
                    && mi.IsGenericMethod
                    && mi.GetParameters().Length == 0)
                {
                    var args = mi.GetGenericArguments();
                    if (args.Length == 1)
                    {
                        var rep = new CodeInstruction(OpCodes.Call, getOpen.MakeGenericMethod(args[0]));
                        rep.labels = ins.labels;
                        rep.blocks = ins.blocks;
                        yield return rep;
                        continue;
                    }
                }
                yield return ins;
            }
        }
    }
}
