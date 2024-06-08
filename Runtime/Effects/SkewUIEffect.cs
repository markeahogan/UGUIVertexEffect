
using System.Collections.Generic;
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    public class SkewUIEffect : UIEffect
    {
        [SerializeField]
        public float _skewY;
        public float _skewX;

        public override Space UIVertexSpace => Space.Local;
        public override bool AffectsPosition => true;

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            vertex = Skew(vertex);
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            var count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = Skew(verts[i]);
            }
        }

        private UIVertex Skew(UIVertex vert)
        {
            var nx = vert.position.x;
            vert.position.y += nx * _skewY;
            var ny = vert.position.y;
            vert.position.x += ny * _skewX;
            return vert;
        }
    }
}