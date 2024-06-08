using System.Collections.Generic;
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    public class MarkAsDirty : UIEffect
    {
        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex) { }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
        }

        private void Update()
        {
            effector.MarkAsDirty(this);
        }
    }
}