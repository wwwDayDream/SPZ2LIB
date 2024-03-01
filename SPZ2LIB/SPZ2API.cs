using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SPZ2LIB
{
    public class SPZ2API : MonoBehaviour
    {
        public static SPZ2API Instance { get; private set; }
        private static List<(ModMetadata, OnModLoaded)> AfterModExecutions { get; } =
            new List<(ModMetadata, OnModLoaded)>();
        
        public List<IMod> Mods { get; } = new List<IMod>();
        private bool isBound = false;

        public static void SendMessageToAllMods(IMod modFrom, string methodName, params object[] arguments)
        {
            const BindingFlags AccessFlag = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            foreach (var (targetMod, targetMethod) in SPZ2API.Instance.Mods.SelectMany(modI =>
                         modI.GetType().GetMethods(AccessFlag).Select(method => (modI, method))))
            {
                var targetMethodParams = targetMethod.GetParameters()
                    .Select(param => param.ParameterType).ToArray();
                var providedParams = arguments.Select(obj => obj.GetType()).ToArray();
                var providedPlusMod = new[] { typeof(IMod) }.Concat(providedParams);

                var isJustParams = targetMethodParams.SequenceEqual(providedParams);
                var isParamsPlusMod = targetMethodParams.SequenceEqual(providedPlusMod);
                if (targetMethod.Name == modFrom.Metadata.Name.Where(char.IsLetter)
                        .Aggregate("", (s, c) => s + c) + methodName && (isJustParams || isParamsPlusMod))
                {
                    targetMethod.Invoke(targetMethod.IsStatic ? null : targetMod, isJustParams ? 
                        arguments : 
                        new object[] { modFrom }.Concat(arguments).ToArray());
                }
            }
        }
        public delegate void OnModLoaded(IMod mod);
        public static void ExecuteAfterModDependency(ModMetadata metadata, OnModLoaded execute)
        {
            if (HasExecutedAlready(metadata, out var mod))
            {
                execute(mod);
                return;
            }
            AfterModExecutions.Add((metadata, execute));
        }

        private static bool HasExecutedAlready(ModMetadata metadata, out IMod modExecuted) =>
            (modExecuted = Instance.Mods.FirstOrDefault(mod => mod.Metadata.Name == metadata.Name && 
                                                               mod.Metadata.Creator == metadata.Creator &&
                                                               mod.Metadata.Version == metadata.Version)) != null;
        private static void InvokeExecuteAfterMod(ModMetadata metadata, IMod mod)
        {
            foreach (var (_, action) in AfterModExecutions.Where(modAction => 
                         modAction.Item1.Name == metadata.Name && 
                         modAction.Item1.Creator == metadata.Creator &&
                         modAction.Item1.Version == metadata.Version))
                action(mod);
        }

        internal void DependenciesLoaded()
        {
            Debug.Log("[SPZ2LIB] MMHookGen loaded. Initializing SPZ2API...");

            isBound = true;
            On.ModsLoader.LoadMod += ModsLoaderOnLoadMod;
            
            Debug.Log("[SPZ2API] Initialized!");
        }

        internal void OnDestroy()
        {
            if (!isBound) return;
            On.ModsLoader.LoadMod -= ModsLoaderOnLoadMod;
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
            hideFlags = HideFlags.HideAndDontSave;
        }
        private void Update()
        {
            foreach (var updatableMod in Mods.Where(mod => mod.GetType().GetInterfaces()
                         .Any(@interface => @interface == typeof(IModExt))))
            {
                // ReSharper disable once SuspiciousTypeConversion.Global
                (updatableMod as IModExt)?.Update();
            }
        }

        private void ModsLoaderOnLoadMod(On.ModsLoader.orig_LoadMod orig, IMod mod, string path)
        {
            Debug.Log("[SPZ2API] Mod load request: " + mod.Metadata.Name);

            Mods.Add(mod);
            
            InvokeExecuteAfterMod(mod.Metadata, mod);
            
            orig(mod, path);
        }
    }
}