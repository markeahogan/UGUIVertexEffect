using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PopupAsylum.UIEffects
{
    public class StaggeredChildrenUIEffect : UIEffect
    {
        [Range(0, 1)]
        public float show = 0;
        float offset = 0;
        public float perChildOffset = 20;

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            if (graphicTransform == transform) { return; }
            vertex = Stagger(graphicTransform, vertex);
        }

        private UIVertex Stagger(RectTransform graphicTransform, UIVertex vertex)
        {
            var childCount = transform.childCount;
            offset = perChildOffset - (show * childCount * perChildOffset);

            var childIndex = GetSiblingIndex(graphicTransform);
            vertex.position += Mathf.Max(0, (offset + perChildOffset * childIndex)) * Vector3.right;

            Color color = vertex.color;
            color.a *= 1 - (childIndex + offset / perChildOffset);
            vertex.color = color;

            return vertex;
        }

        int GetSiblingIndex(Transform t)
        {
            while (t.parent != transform && t.parent != null) { t = t.parent; }
            return t.GetSiblingIndex();
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] = Stagger(graphicTransform, verts[i]);
            }
        }
    }
}