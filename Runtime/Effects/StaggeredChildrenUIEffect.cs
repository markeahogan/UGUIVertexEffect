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

            var childCount = transform.childCount;
            var childIndex = GetSiblingIndex(graphicTransform);
            vertex = Stagger(vertex, childCount, childIndex);
        }

        private UIVertex Stagger(UIVertex vertex, int childCount, int childIndex)
        {
            var pcof = Mathf.Abs(perChildOffset);
            offset = pcof - (show * childCount * pcof);

            vertex.position += Mathf.Max(0, (offset + pcof * childIndex)) * (perChildOffset >= 0 ? Vector3.right : Vector3.left);

            Color color = vertex.color;
            color.a *= 1 - (childIndex + offset / pcof);
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
            var childCount = transform.childCount;
            var childIndex = GetSiblingIndex(graphicTransform);

            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] = Stagger(verts[i], childCount, childIndex);
            }
        }
    }
}