using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public sealed class CombatRuntimeRockLifetime : MonoBehaviour
    {
        public CombatRuntimeRock Owner { private get; set; }

        private void OnDisable()
        {
            if (Owner != null && Owner.Rock == gameObject && Owner.Placement.HasValue) Owner.Clear();
        }

        private void OnDestroy() => OnDisable();
    }
}
