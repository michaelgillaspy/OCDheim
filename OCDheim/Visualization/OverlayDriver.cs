using UnityEngine;

using static OCDheim.PlayerHelpers;

namespace OCDheim
{
    // Ticks the current placement ghost's OverlayVisualizer from the plugin's own persistent
    // GameObject. The ghost can't tick itself: Valheim toggles its active state within the frame,
    // so Unity skips it in the update list and its Update() never runs while the inventory is
    // closed - which is exactly when the overlay needs to react to scroll input.
    public class OverlayDriver : MonoBehaviour
    {
        private void Update()
        {
            if (!player || !player.m_placementGhost) { return; }

            var overlay = player.m_placementGhost.GetComponent<OverlayVisualizer>();
            if (overlay)
            {
                overlay.Tick();
            }
        }
    }
}
