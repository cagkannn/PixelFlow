using UnityEngine;

namespace PixelFlow.Runtime
{
    // The selectable collider belongs to the same 3D UFO used on the road.
    public sealed class ShipPointer : MonoBehaviour
    {
        public string ShipId;
        public bool CanSelect;
    }
}
