#if NOESIS
using UnityEngine;

namespace NeoFastRider.UI
{
    // Registra los tipos custom del Visor en el sistema de tipos de Noesis
    // usando la API correcta de la versión embebida (Noesis.Extend.EnsureNativeType).
    [UnityEngine.Scripting.Preserve]
    public static class HelmetVisorNoesisProvider
    {
        // SubsystemRegistration: primero de todos los RuntimeInitialize.
        // Garantiza que los tipos están en el sistema Noesis antes de que
        // NoesisView.Awake() parsee el XAML.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAll()
        {
            if (!Noesis.Extend.Initialized) return;

            Noesis.Extend.EnsureNativeType(typeof(HelmetVisorViewModel));
            Noesis.Extend.EnsureNativeType(typeof(RPMToArcGeometryConverter));
            Noesis.Extend.EnsureNativeType(typeof(BoolToVisibilityConverter));
        }
    }
}
#endif
