using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SPZ2LIB
{
    internal class Patcher : IPatcher
    {
        internal class MonoModAwaiter : MonoBehaviour
        {
            // SPZ2MMHookGen will use the below call to notify it's initialization.
            // UnityEngine.Object.FindObjectOfType<Preloader>()?.SendMessage("MonoModInitialized");
            // Or Awake will if it's already loaded when SPZ2LIB loads
            internal void MonoModInitialized()
            {
                SPZ2API.Instance.DependenciesLoaded();
                DestroyImmediate(this);
            }
            internal void Awake()
            {
                // Here we detect that MonoMod has added it's own flag MonoBehaviour to the GameObject so it's already loaded.
                // On my system, however, it waits for the event call because SPZ2LIB::Patcher loads first.
                if (gameObject.GetComponents<Component>()
                    .Any(comp => comp.GetType().FullName == "SPZ2MMHookGen.Patcher+MonoModFlag"))
                    MonoModInitialized();
            }
        }
        
        internal static GameObject LibObject { get; private set; }
        public void Patch()
        {
            Debug.Log("[SPZ2LIB] Waiting for SPZ2MMHookGen...");
            Object.FindObjectOfType<Preloader>().gameObject.AddComponent<MonoModAwaiter>();
            
            LibObject = new GameObject("SPZ2LIB", typeof(SPZ2API));
        }
    }
}