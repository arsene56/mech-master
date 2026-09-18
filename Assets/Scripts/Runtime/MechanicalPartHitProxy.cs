using UnityEngine;

namespace MechMaster.Runtime
{
    public sealed class MechanicalPartHitProxy : MonoBehaviour
    {
        public MechanicalPartView Owner { get; private set; }

        public void Initialize(MechanicalPartView owner)
        {
            Owner = owner;
        }
    }
}
