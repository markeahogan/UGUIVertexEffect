using PopupAsylum.UIEffects.Extensions;
using System.Collections.Generic;
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    public class UVRectUIEffect : UIEffect
    {
        [SerializeField]
        RectTransform _source;

        public override Space UIVertexSpace => Space.Local;

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            SizeAndOrigin(_source, out var size, out var origin);
            vertex = UpdateVert(vertex, size, origin);
        }

        private UIVertex UpdateVert(UIVertex vertex, Vector2 size, Vector2 origin)
        {
            ConvertSpace(ref vertex, rectTransform, _source);
            vertex.uv0 = (vertex.position.xy() - origin) / size;
            ConvertSpace(ref vertex, _source, rectTransform);
            return vertex;
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            SizeAndOrigin(_source, out var size, out var origin);
            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] = UpdateVert(verts[i], size, origin);
            }
        }
    }
}
