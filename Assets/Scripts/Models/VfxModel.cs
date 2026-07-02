using OptimizationGame.MonoBehaviours;

namespace OptimizationGame.Models
{
    /// <summary>
    /// Estado runtime mínimo de un VFX pooled activo. Clase pura (NO MonoBehaviour):
    /// asocia la EntityView tomada del pool con la key para devolverla y el tiempo de
    /// vida restante. La posición vive en el transform de la view, no se duplica acá.
    /// </summary>
    public class VfxModel
    {
        public string PoolKey;
        public EntityView View;
        public float RemainingLifetime;

        public VfxModel(string poolKey, EntityView view, float lifetime)
        {
            PoolKey = poolKey;
            View = view;
            RemainingLifetime = lifetime;
        }
    }
}
