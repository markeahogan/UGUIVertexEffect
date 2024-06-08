using System.Collections.Generic;
using UnityEngine;

namespace PopupAsylum.UIEffects
{
    public class FeatheredEdgeUIEffect : UIEffect
    {
        [SerializeField]
        float _top, _bottom, _left, _right;
        [SerializeField]
        int _divisions = 0;

        public override Space UIVertexSpace => Space.Local;

        public override bool AffectsVertex => _top > 0 || _bottom > 0 || _left > 0 || _right > 0;

        public override void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            base.AddDivisions(graphicTransform, list);

            SizeAndOrigin(out var size, out var origin);

            Vector4 borders = GetBorders(_bottom, _top, size.y);
            CreateSlices(borders[0], borders[1], 1);
            CreateSlices(borders[3], borders[2], 1);

            borders = GetBorders(_left, _right, size.x);
            CreateSlices(borders[0], borders[1], 0);
            CreateSlices(borders[3], borders[2], 0);

            void CreateSlices(float from, float to, int axis)
            {
                if (from == to) return;
                var normal = axis == 1 ? Vector3.up : Vector3.right;
                var divisions = System.Math.Max(1, _divisions + 1);

                for (int i = 1; i <= divisions; i++)
                {
                    float pos = Mathf.Lerp(from, to, i / (float)divisions);
                    Plane plane = new Plane(normal, origin[axis] + pos);
                    ConvertSpace(ref plane, transform, graphicTransform);
                    list.Add(plane);
                }
            }
        }

        protected override void AddClippingPlanes(RectTransform graphicTransform, List<Plane> divisions)
        {
            base.AddClippingPlanes(graphicTransform, divisions);

            SizeAndOrigin(out var size, out var origin);
            
            Plane plane = new Plane(Vector3.right, origin.x + size.x);
            ConvertSpace(ref plane, rectTransform, graphicTransform);
            divisions.Add(plane);

            plane = new Plane(Vector3.up, origin.y + size.y);
            ConvertSpace(ref plane, rectTransform, graphicTransform);
            divisions.Add(plane);

            //when using negative axis the distance needs to be reversed
            plane = new Plane(Vector3.left, -origin.x);
            ConvertSpace(ref plane, rectTransform, graphicTransform);
            divisions.Add(plane);            

            plane = new Plane(Vector3.down, -origin.y);
            ConvertSpace(ref plane, rectTransform, graphicTransform);
            divisions.Add(plane);
        }

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            SizeAndOrigin(out var size, out var origin);
            Vector4 bordersX = GetBorders(_left, _right, size.x);
            var bordersY = GetBorders(_bottom, _top, size.y);

            vertex = FeatherEdge(origin, bordersX, bordersY, vertex);
        }

        private static UIVertex FeatherEdge(Vector2 origin, Vector4 bordersX, Vector4 bordersY, UIVertex vertex)
        {
            var pos2D = vertex.position - (Vector3)origin;
            float dist = 1;
            AddAlpha(bordersX[0], bordersX[1], pos2D.x);
            AddAlpha(bordersX[3], bordersX[2], pos2D.x);

            AddAlpha(bordersY[0], bordersY[1], pos2D.y);
            AddAlpha(bordersY[3], bordersY[2], pos2D.y);

            void AddAlpha(float from, float to, float t)
            {
                if (from == to) return;
                dist *= Mathf.InverseLerp(from, to, t);
            }

            vertex.color *= new Color(1, 1, 1, dist);
            return vertex;
        }

        Vector4 GetBorders(float minBorder, float maxBorder, float size)
        {
            Vector4 result = default;
            result[0] = 0;
            result[1] = Mathf.Max(0, minBorder);
            result[2] = Mathf.Min(size, size - maxBorder);
            result[3] = size;
            return result;
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            SizeAndOrigin(out var size, out var origin);
            Vector4 bordersX = GetBorders(_left, _right, size.x);
            var bordersY = GetBorders(_bottom, _top, size.y);

            var count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = FeatherEdge(origin, bordersX, bordersY, verts[i]);
            }
        }
    }
}