using UnityEngine;

namespace PopupAsylum.UIEffects
{
    [ExecuteAlways]
    public class UIDividerHints : MonoBehaviour
    {
        public UIDivider.Flags flags = UIDivider.Flags.NoDivisions;

        private void OnEnable() => UIMeshModifier.Repair(gameObject);
        private void OnDisable() => UIMeshModifier.Repair(gameObject);
    }
}
