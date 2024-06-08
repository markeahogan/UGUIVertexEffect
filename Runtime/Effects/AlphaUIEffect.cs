using System.Collections.Generic;
using UnityEngine;


namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// Bascially the same as CanvasGroup
    /// </summary>
    public class AlphaUIEffect : UIEffect
    {
        public float alpha = 1;

        public override void ModifyVertex(RectTransform rectTransform, ref UIVertex vertex)
        {
            vertex = ApplyAlpha(vertex);
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            int count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = ApplyAlpha(verts[i]);
            }
        }

        private UIVertex ApplyAlpha(UIVertex vertex)
        {
            vertex.color = new Color32(vertex.color.r, vertex.color.g, vertex.color.b, (byte)(vertex.color.a * alpha));
            return vertex;
        }
    }
}