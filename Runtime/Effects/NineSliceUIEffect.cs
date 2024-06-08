using System.Collections.Generic;
using UnityEngine;


namespace PopupAsylum.UIEffects
{
    public class NineSliceUIEffect : UIEffect
    {
        [SerializeField]
        Vector2 _referenceResolution = new Vector2(200, 200);
        [SerializeField]
        float _top, _bottom, _left, _right;

        public override Space UIVertexSpace => Space.Local;

        private void Reset()
        {
            _affect = Affect.Self;
    }

        public override void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            base.AddDivisions(graphicTransform, list);

            var size = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
            var origin = -rectTransform.pivot * size;

            var topPlane = new Plane(Vector3.up, origin.y + (size.y * (1 - _top)));
            ConvertSpace(ref topPlane, transform, graphicTransform);
            list.Add(topPlane);

            var bottomPlane = new Plane(Vector3.up, origin.y + (size.y * _bottom));
            ConvertSpace(ref bottomPlane, transform, graphicTransform);
            list.Add(bottomPlane);

            var rightPlane = new Plane(Vector3.right, origin.x + (size.x * (1 - _right)));
            ConvertSpace(ref rightPlane, transform, graphicTransform);
            list.Add(rightPlane);

            var leftPlane = new Plane(Vector3.right, origin.x + (size.x * _left));
            ConvertSpace(ref leftPlane, transform, graphicTransform);
            list.Add(leftPlane);
        }

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            SizeAndOrigin(out var size, out var origin);
            vertex = NineSlice(vertex, size, origin);
        }

        private UIVertex NineSlice(UIVertex vertex, Vector2 size, Vector2 origin)
        {
            vertex.position.x = Slice(vertex.position.x, size.x, origin.x, _referenceResolution.x, _left, _right);
            vertex.position.y = Slice(vertex.position.y, size.y, origin.y, _referenceResolution.y, _bottom, _top);
            return vertex;
        }

        private float Slice(float position, float size, float origin, float referenceResolution, float min, float max)
        {
            var normalized = (position - origin) / size;

            if (normalized <= min)
            {
                var horiz = normalized / min;
                var total = min * referenceResolution;
                return total * horiz + origin;
            }
            else if (1 - normalized <= max)
            {
                var horiz = (1 - normalized) / max;
                var total = max * referenceResolution;
                return origin + size - total * horiz;
            }
            else
            {
                var horiz = (normalized - min) / ((1 - max) - min);
                var total = size - (max + min) * referenceResolution;
                return origin + min * referenceResolution + horiz * total;
            }
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            SizeAndOrigin(out var size, out var origin);
            int count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = NineSlice(verts[i], size, origin);
            }
        }
    }
}