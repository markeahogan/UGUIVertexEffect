using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using Channels = UnityEngine.AdditionalCanvasShaderChannels;

namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// Divides and Clips a UGUI mesh along a set of planes</br>
    /// To begin call Start() passing the VertexHelper or a stream of UIVertexs</br>
    /// Call Clip() and Divide() to create additional edges along planes</br>
    /// Call ForEach() to make per vertex changes</br>
    /// Finally, call Finish() to submit the updated geometry
    /// </summary>
    public class UIDivider
    {
        private const Flags _noFlags = 0;

        private List<UIVertex> _verts = new List<UIVertex>();
        private List<int> _indicies = new List<int>();
        private List<UIPolygon> _polygons = new List<UIPolygon>();

        private VertexHelper _helper;
        private bool _hasHelper;
        private bool _divided = false;

        private bool _noDivisions = false;
        private bool _preserveOrder = false;
        private bool _quads = false;

        private Channels _channels = Channels.None;

        /// <summary>
        /// Begins a set of division instructions on a UI element. Call Clip()/Divide() followed by Finish() to update the UI elements geometry
        /// </summary>
        /// <param name="helper">The vertexHelper of the UI element to divide</param>
        /// <param name="flags">Optional flags which can be used to tweak performance</param>
        /// <param name="channels">Optional additional channels needing to be included in the resulting mesh, e.g. TMPro needs normals</param>
        public void Start(VertexHelper helper, Flags flags = _noFlags, Channels channels = Channels.None)
        {
            _helper = helper;
            _hasHelper = true;
            CommonSetup(flags, channels);

            ConvertStreamToPolygons();
            _divided = true;
        }

        /// <summary>
        /// Begins a set of division instructions on a UI element supplied as a quad stream. Call Clip()/Divide() followed by Finish() to update the UI elements geometry
        /// </summary>
        /// <param name="quadStream">The geomrety to clip/divide</param>
        /// <param name="flags">Optional flags which can be used to tweak performance</param>
        /// <param name="channels">Optional additional channels needing to be included in the resulting mesh, e.g. TMPro needs normals</param>
        public void StartQuadStream(List<UIVertex> quadStream, Flags flags = _noFlags, Channels channels = Channels.None)
        {
            CommonSetup(flags | Flags.StartAsQuads, channels);

            _hasHelper = false;
            _verts.AddRange(quadStream);

            int vertCount = _verts.Count;
            for (int quadIndex = 0; quadIndex < vertCount; quadIndex += 4)
            {
                _polygons.Add(UIPolygon.GetTemporaryFromQuadStream(_verts, quadIndex));
            }

            _divided = true; //HACK for tmpro
        }

        private void CommonSetup(Flags flags, Channels channels)
        {
            _verts.Clear();
            _indicies.Clear();
            _polygons.Clear();
            _divided = false;

            _preserveOrder = (flags & Flags.PreserveSorting) != 0;
            _quads = (flags & Flags.StartAsQuads) != 0;
            _noDivisions = (flags & Flags.NoDivisions) != 0;
            _channels = channels;
        }

        /// <summary>
        /// Removes parts of the geometry that are on the positive side of any plane
        /// </summary>
        /// <param name="clippingPlanes">A set of planes that define the area to be culled, they should form a convex shape</param>
        /// <param name="localCorners">An optional 4 vector array defining a rect, planes which do not intersect the rect will be ignored for optimization</param>
        public void Clip(List<Plane> clippingPlanes, Vector3[] localCorners = null)
        {
            DivideAndClip(clippingPlanes, localCorners, true);
        }

        /// <summary>
        /// Adds divisions to the geometry along where the plane intersects
        /// </summary>
        /// <param name="divisionPlanes">The planes to divide along. Note the List will be sorted in the process!</param>
        /// <param name="localCorners">An optional 4 vector array defining a rect, planes which do not intersect the rect will be ignored for optimization</param>
        public void Divide(List<Plane> divisionPlanes, Vector3[] localCorners = null)
        {
            DivideAndClip(divisionPlanes, localCorners, false);
        }

        private void DivideAndClip(List<Plane> planes, Vector3[] localCorners = null, bool clip = false)
        {
            if (_noDivisions) { return; }

            if (!clip) { planes.RemoveAll(x => !PlaneIntersects(x, localCorners)); }

            if (planes.Count == 0) { return; }

            planes.Sort(SortPlanes);

            if (_hasHelper && !_divided && _polygons.Count == 0)
            {
                ConvertStreamToPolygons();
            }

            Profiler.BeginSample("UIDivider.Divide");
            int planesStart = 0;
            int planesEnd = 1;
            while (planesEnd <= planes.Count)
            {
                // we can skip massive amounts of checks if we divide parallel planes in order
                // since we know any of the polygons behind plane 1 are also behind plane 2
                planesEnd = GetParallelRange(planes, planesStart);

                // when preserveSorting is off we will add the polygons behind each plance to the end of the list (normally O(1)),
                // we know these wont be divided further, cache the count so the loop doesnt iterate over them
                var polyCount = _polygons.Count;

                for (int i = 0; i < polyCount; i++)
                {
                    var dividingPolygon = _polygons[i];

                    // flag to early exit when we reach the end of the planes that affect the polygon
                    var isDividing = false;
                    var breaker = 0;

                    for (int planeIndex = planesStart; planeIndex < planesEnd; planeIndex++)
                    {
                        var plane = planes[planeIndex];

                        bool side = true;
                        if (dividingPolygon.Divide(plane, out var back, out var front, out side, _channels, clip))
                        {
                            // we have 2 new polys, one behind the plane and one in front
                            // which need to be inserted into the polygon list, replacing the old one
                            // the front poly may get divided by the next plane, but the back one 100% wont

                            UIPolygon.ReleaseTemporary(dividingPolygon);
                            dividingPolygon = front;

                            if (!clip)
                            {
                                if (_preserveOrder)
                                {
                                    // replace the old polygon with the back, which wont get divided again
                                    _polygons[i] = back;
                                    // insert the front after the back and shift the iterator forward
                                    _polygons.Insert(++i, front);
                                    polyCount++;
                                }
                                else
                                {
                                    // replace the old polygon with the front, which may get divided again
                                    _polygons[i] = front;
                                    // for the back, which wont get divided again
                                    // it's faster to add to the end of the list
                                    // but it breaks sorting on SVGs
                                    _polygons.Add(back);
                                }
                            }
                            else
                            {
                                _polygons[i] = front;
                            }

                            isDividing = true;
                            _divided = true;
                        }
                        else if (isDividing)
                        {
                            // this plane didnt divide the polygon but the previous one did
                            // we've reached the end of the planes that intersect this polygon
                            if (breaker++ > 1) break; // HACK breaker is fix for bug on gradient at 252 degrees
                        }
                        else if (clip && !side)
                        {
                            _polygons.RemoveAt(i--);
                            polyCount--;
                            _divided = true;
                        }
                    }
                }

                planesStart = planesEnd;
                planesEnd = planesEnd + 1;
            }

            Profiler.EndSample();                
        }

        private static int GetParallelRange(List<Plane> planes, int start)
        {
            var normal = planes[start].normal;
            var count = planes.Count;
            for (int i = start + 1; i < count; i++)
            {
                if (planes[i].normal != normal)
                {
                    return i;
                }
            }
            return count;
        }

        private void ConvertStreamToPolygons()
        {
            _helper.GetUIVertexStream(_verts);
            int vertCount = _verts.Count;
            int increment = _quads ? 6 : 3;
            for (int triangleIndex = 0; triangleIndex < vertCount; triangleIndex += increment)
            {
                _polygons.Add(UIPolygon.GetTemporaryFromTriangleStream(_verts, triangleIndex, _quads));
            }
        }

        /// <summary>
        /// Sorts planes by normal then distance
        /// </summary>
        private int SortPlanes(Plane a, Plane b)
        {
            var x = a.normal.x.CompareTo(b.normal.x);
            if (x != 0) { return x; }
            var y = a.normal.y.CompareTo(b.normal.y);
            if (y != 0) { return y; }
            var z = a.normal.z.CompareTo(b.normal.z);
            if (z != 0) { return z; }
            return a.distance.CompareTo(b.distance);
        }

        /// <summary>
        /// Performs the specified func for each vertex of the mesh, the value returned replaces the original.
        /// </summary>
        public void ForEach(Func<UIVertex, UIVertex> vertexAction)
        {
            Profiler.BeginSample("UIDivider.ForEach");
            // if we dont end up dividing anything we'll use the original
            // verts so they need updating too
            if (!_divided && _hasHelper)
            {
                UIVertex v = default;
                var count = _helper.currentVertCount;
                //Parallel.For(0, count, i =>
                for (int i = 0; i < count; i++)
                {
                    _helper.PopulateUIVertex(ref v, i);
                    v = vertexAction(v);
                    _helper.SetUIVertex(v, i);
                }
                //);
            }

            var count2 = _verts.Count;
            //Parallel.For(0, count2, i =>
            for (int i = 0; i < count2; i++)
            {
                _verts[i] = vertexAction(_verts[i]);
            }
            //);
            Profiler.EndSample();
        }

        public List<UIVertex> GetVertices() => _verts;

        /// <summary>
        /// Replaces the VertexHelper mesh with the updated geometry
        /// </summary>
        public void Finish()
        {
            Profiler.BeginSample("UIDivider.Finish");
            if (_divided)
            {
                for (int i = 0; i < _polygons.Count; i++)
                {
                    _polygons[i].Triangluate(_indicies);
                }

                _helper.Clear();
                _helper.AddUIVertexStream(_verts, _indicies);
            }

            for (int i = 0; i < _polygons.Count; i++)
            {
                UIPolygon.ReleaseTemporary(_polygons[i]);
            }
            _polygons.Clear();

            _indicies.Clear();
            _helper = null;
            _divided = false;
            Profiler.EndSample();
        }

        /// <summary>
        /// If the geometry has been modified, fills the specified lists with the verts and triangles of the modified </br>
        /// Returns if the geometry has been modified
        /// </summary>
        /// <param name="verts">The list to clear and populate with verts</param>
        /// <param name="indicies">The list to clear and populate with indicies</param>
        /// <returns>true if the geometry has been modified, otherwise false</returns>
        public bool Finish(List<UIVertex> verts, List<int> indicies)
        {
            if (_divided)
            {
                for (int i = 0; i < _polygons.Count; i++)
                {
                    _polygons[i].Triangluate(_indicies);
                }

                verts.Clear();
                indicies.Clear();
                verts.AddRange(_verts);
                indicies.AddRange(_indicies);
            }

            for (int i = 0; i < _polygons.Count; i++)
            {
                UIPolygon.ReleaseTemporary(_polygons[i]);
            }
            _polygons.Clear();

            _indicies.Clear();
            _helper = null;

            var temp = _divided;
            _divided = false;
            return temp;
        }

        /// <summary>
        /// If the geometry has been modified, fills the specified lists with the verts and triangles of the modified </br>
        /// Returns if the geometry has been modified
        /// </summary>
        /// <param name="verts">The list to clear and populate with verts</param>
        /// <param name="indicies">The list to clear and populate with indicies</param>
        /// <returns>true if the geometry has been modified, otherwise false</returns>
        public bool Finish(List<UIVertex> verts, List<ushort> indicies)
        {
            if (_divided)
            {
                for (int i = 0; i < _polygons.Count; i++)
                {
                    _polygons[i].Triangluate(_indicies);
                }

                verts.Clear();
                indicies.Clear();
                verts.AddRange(_verts);
                foreach(var index in _indicies) indicies.Add(Convert.ToUInt16(index));
            }

            for (int i = 0; i < _polygons.Count; i++)
            {
                UIPolygon.ReleaseTemporary(_polygons[i]);
            }
            _polygons.Clear();

            _indicies.Clear();
            _helper = null;

            var temp = _divided;
            _divided = false;
            return temp;
        }

        /// <summary>
        /// Adds divisions to the VertexHelper</br>
        /// If multiple divisions will be made in stages then calling Start(), Divide() and Finish() is more optimal
        /// </summary>
        public void Divide(VertexHelper vh, List<Plane> planes, Flags flags = _noFlags, Channels channels = Channels.None)
        {
            Start(vh, flags, channels);
            Divide(planes);
            Finish();
        }

        static UIVertex CreateLerpVertex(UIVertex vertex1, UIVertex vertex2, float lerp, Channels channels = Channels.None)
        {
            UIVertex result = vertex1;
            result.position = Vector3.Lerp(vertex1.position, vertex2.position, lerp);
            result.color = Color.Lerp(vertex1.color, vertex2.color, lerp);
            result.uv0 = Vector4.Lerp(vertex1.uv0, vertex2.uv0, lerp);

            if ((channels & Channels.Normal) != 0)
            {
                result.normal = Vector3.Lerp(vertex1.normal, vertex2.normal, lerp);
            }
            if ((channels & Channels.Tangent) != 0)
            {
                result.tangent = Vector4.Lerp(vertex1.tangent, vertex2.tangent, lerp);
            }
            if ((channels & Channels.TexCoord1) != 0)
            {
                result.uv1 = Vector4.Lerp(vertex1.uv1, vertex2.uv1, lerp);
            }
            if ((channels & Channels.TexCoord2) != 0)
            {
                result.uv2 = Vector4.Lerp(vertex1.uv2, vertex2.uv2, lerp);
            }
            if ((channels & Channels.TexCoord3) != 0)
            {
                result.uv3 = Vector4.Lerp(vertex1.uv3, vertex2.uv3, lerp);
            }

            return result;
        }

        /// <summary>
        /// Returns true if the plane crosses the polygon formed by the corners
        /// Used to early exit when dividing a graphic when we can see no polygons will be divided
        /// </summary>
        static bool PlaneIntersects(Plane plane, Vector3[] corners)
        {
            // without the information, assume that it intersects
            if (corners == null || corners.Length != 4) { return true; }

            bool side = plane.GetSide(corners[0]);
            for (int i = 1; i < 4; i++)
            {
                if (side != plane.GetSide(corners[i]))
                {
                    return true;
                }
            }
            return false;
        }

        class UIPolygon
        {
            private static List<UIPolygon> _pool = new List<UIPolygon>();

            private List<UIVertex> _vertSource;
            private List<int> _vertIndicies;

            public UIVertex this[int i] => _vertSource[_vertIndicies[i]];

            public int Count => _vertIndicies.Count;

            public static UIPolygon GetTemporaryFromTriangleStream(List<UIVertex> verts, int startIndex, bool quad = false)
            {
                UIPolygon result = GetTemporary(verts);
                result._vertIndicies.Add(startIndex);
                result._vertIndicies.Add(startIndex + 1);
                result._vertIndicies.Add(startIndex + 2);
                if (quad) { result._vertIndicies.Add(startIndex + 4); }
                return result;
            }

            public static UIPolygon GetTemporaryFromQuadStream(List<UIVertex> verts, int startIndex)
            {
                UIPolygon result = GetTemporary(verts);
                result._vertIndicies.Add(startIndex);
                result._vertIndicies.Add(startIndex + 1);
                result._vertIndicies.Add(startIndex + 2);
                result._vertIndicies.Add(startIndex + 3);
                return result;
            }

            public static UIPolygon GetTemporary(List<UIVertex> verts)
            {
                UIPolygon result;
                if (_pool.Count > 0)
                {
                    result = _pool[_pool.Count - 1];
                    result._vertSource = verts;
                    result._vertIndicies?.Clear();
                    _pool.RemoveAt(_pool.Count - 1);
                }
                else
                {
                    result = new UIPolygon(verts);
                }

                if (result._vertIndicies == null) result._vertIndicies = new List<int>();
                return result;
            }

            public static void ReleaseTemporary(UIPolygon polygon)
            {
                _pool.Add(polygon);
            }

            private UIPolygon(List<UIVertex> vertSource)
            {
                _vertSource = vertSource;
                _vertIndicies = null;
            }

            /// <summary>
            /// If the plane crosses this polygon, divides the polygon by the plane
            /// Assumes the polygon is convex and planar
            /// </summary>
            public bool Divide(Plane plane, out UIPolygon back, out UIPolygon front, out bool side, Channels channels = Channels.None, bool clip = false)
            {
                side = true;
                // find the first edge that the plane crosses
                for (int edge1 = 0; edge1 < Count - 1; edge1++)
                {
                    var edge1Start = this[edge1];
                    var edge1End = this[edge1 + 1];

                    if (LineCastInterpolant(plane, edge1Start.position, edge1End.position, out var t, out side))
                    {
                        //find the second edge the plane crosses
                        for (int edge2 = edge1 + 1; edge2 < Count; edge2++)
                        {
                            var edge2Start = this[edge2];
                            var edge2End = this[edge2 == Count - 1 ? 0 : edge2 + 1]; //wrap round on last edge

                            if (LineCastInterpolant(plane, edge2Start.position, edge2End.position, out var t2, out var _))
                            {
                                _vertSource.Add(CreateLerpVertex(edge1Start, edge1End, t, channels));
                                _vertSource.Add(CreateLerpVertex(edge2Start, edge2End, t2, channels));

                                if (!clip)
                                {
                                    if (side)
                                    {
                                        front = CreatePolygon(edge2, edge1);
                                        back = CreatePolygon(edge1, edge2);
                                    }
                                    else
                                    {
                                        back = CreatePolygon(edge2, edge1);
                                        front = CreatePolygon(edge1, edge2);
                                    }
                                }
                                else
                                {
                                    back = front = side ? CreatePolygon(edge2, edge1) : CreatePolygon(edge1, edge2);
                                }
                                return true;
                            }
                        }

                        break; // found a first edge but no second edge, shouldn't happen, maybe coplanar?
                    }
                }                
                // no intersection with plane
                back = front = this;
                return false;
            }

            /// <summary>
            /// Returns true if the line intersects the plane, t will be the interpolant
            /// </summary>
            bool LineCastInterpolant(Plane plane, Vector3 start, Vector3 end, out float t, out bool side)
            {
                float startDotN = -Vector3.Dot(start, plane.normal);
                float endDotN = -Vector3.Dot(end, plane.normal);

                side = startDotN > plane.distance;

                if ((side && endDotN > plane.distance) ||
                   (!side && endDotN <= plane.distance))
                {
                    t = 0;
                    return false;
                }

                Vector3 line = end - start;
                float lineMagnitude = line.magnitude;
                Vector3 lineNormalized = line / lineMagnitude;
                float linePlaneAlignment = Vector3.Dot(lineNormalized, plane.normal);

                if (Mathf.Approximately(linePlaneAlignment, 0))
                {
                    t = 0;
                    return false;
                }

                float distanceFromStartToPlane = startDotN - plane.distance;
                t = (distanceFromStartToPlane / linePlaneAlignment) / lineMagnitude;
                return true;
            }

            /// <summary>
            /// Assumed to be called after adding 2 new verts to the list, 
            /// creates a polygon starting from the 2 new verts and iterating over the indicies between start and end
            /// </summary>
            private UIPolygon CreatePolygon(int startExclusive, int endInclusive)
            {
                var polygon = GetTemporary(_vertSource);
                if (polygon._vertIndicies == null) polygon._vertIndicies = new List<int>();

                bool reverseDivisionWinding = startExclusive > endInclusive;

                polygon._vertIndicies.Add(_vertSource.Count - (reverseDivisionWinding ? 2 : 1));
                polygon._vertIndicies.Add(_vertSource.Count - (reverseDivisionWinding ? 1 : 2));

                while (startExclusive != endInclusive)
                {
                    startExclusive = (startExclusive + 1) % Count;
                    polygon._vertIndicies.Add(_vertIndicies[startExclusive]);
                }

                return polygon;
            }

            public void Triangluate(List<int> indicies)
            {
                for (int i = 1; i < _vertIndicies.Count - 1; i++)
                {
                    indicies.Add(_vertIndicies[0]);
                    indicies.Add(_vertIndicies[i]);
                    indicies.Add(_vertIndicies[i + 1]);
                }
            }
        }

        /// <summary>
        /// Optional hints for optimizing the dividing process
        /// </summary>
        [Flags]
        public enum Flags
        {
            /// <summary>
            /// Forces the order of the geometry triangles to be preserved, to maintain correct draw order, used for SVGImage
            /// </summary>
            PreserveSorting = 1 << 0,
            /// <summary>
            /// Indicates that the VertexHelper only contains Quads, as an optimization
            /// </summary>
            StartAsQuads = 1 << 1,
            /// <summary>
            /// Indicates that no divisions should occur
            /// </summary>
            NoDivisions = 1 << 2
        }
    }
}