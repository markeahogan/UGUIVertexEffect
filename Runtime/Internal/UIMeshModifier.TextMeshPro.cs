using System.Collections.Generic;
#if TMPRO_INCLUDED
using TMPro;
#endif
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace PopupAsylum.UIEffects
{
    public partial class UIMeshModifier
    {
#if TMPRO_INCLUDED
        private TMP _text;
#endif

        /// <summary>
        /// Makes the text update, if this graphic is text
        /// </summary>
        /// <returns>true if this graphic is text</returns>
        private bool SetTextDirty()
        {
#if TMPRO_INCLUDED
            if (_graphic is TMP_Text text)
            {
                AffectTextMesh();
                return true;
            }
#endif
            return false;
        }

        private void SetTextCallbacksRegistered(bool register)
        {
#if TMPRO_INCLUDED
            if (_graphic is TMP_Text text)
            {
                text.OnPreRenderText -= UpdateStream;
                TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(AffectTextMesh);
                Canvas.willRenderCanvases -= UpdateCanvasMesh;

                if (!register) return;

                // on pre render gives us the text as TMPro wanted it to be displayed, we cache it
                text.OnPreRenderText += UpdateStream;
                // once the text has been updated, thats when we modify our copy of it and send it to the canvas renderer
                TMPro_EventManager.TEXT_CHANGED_EVENT.Add(AffectTextMesh);
                // HACK internal knowledge here, tmpro reassigns it's mesh to the canvasRenderer in Canvas.willRenderCanvases
                // we need to assign it back to our modified version
                Canvas.willRenderCanvases += UpdateCanvasMesh;

                _text._canvasRenderer = GetComponent<CanvasRenderer>();
                if (!_text._mesh)
                {
                    _text.Init();
                }
            }
#endif
        }

#if TMPRO_INCLUDED
        /// <summary>
        /// Copies the original mesh of the text into a quad stream "_originalStream",
        /// this allows us to modify the original text multiple times e.g. when an effect changes but the text hasn't
        /// </summary>
        private void UpdateStream(TMP_TextInfo textInfo)
        {
            _text._originalStream.Clear();

            if (textInfo.characterCount == 0)
            {
                _text._modified = false;
                return;
            }

            var meshInfos = textInfo.meshInfo;
            for (int i = 0; i < meshInfos.Length; i++)
            {
                TMP_MeshInfo meshInfo = meshInfos[i];

                for (int j = 0; j < meshInfo.triangles.Length; j += 6)
                {
                    _text._originalStream.Add(GetVert(meshInfo.triangles[j]));
                    _text._originalStream.Add(GetVert(meshInfo.triangles[j + 1]));
                    _text._originalStream.Add(GetVert(meshInfo.triangles[j + 2]));
                    _text._originalStream.Add(GetVert(meshInfo.triangles[j + 4]));
                }

                // converts a vert from tmpro to UIVertex
                UIVertex GetVert(int index)
                {
                    return new UIVertex()
                    {
                        position = meshInfo.vertices[index],
                        normal = meshInfo.normals[index],
                        color = meshInfo.colors32[index],
                        uv0 = meshInfo.uvs0[index],
                        uv1 = meshInfo.uvs2[index],
                        tangent = meshInfo.tangents[index]
                    };
                }
            }
        }

        private void AffectTextMesh(Object textThatUpdated)
        {
            if (textThatUpdated == _graphic) AffectTextMesh();
        }

        /// <summary>
        /// Divides and effects the original stream into a new mesh, then sets that as the canvas renderers mesh
        /// </summary>
        private void AffectTextMesh()
        {
            //if (Time.frameCount == _text.lastUpdate && Application.isPlaying) return;
            _text.lastUpdate = Time.frameCount;

            if (_text._originalStream == null || _text._originalStream.Count == 0)
            {
                _text._modified = false;
                UpdateCanvasMesh();
                return;
            }

            Profiler.BeginSample("UIMeshModifier.AffectTextMesh");

            _divider.StartQuadStream(_text._originalStream, _hintFlags.GetValueOrDefault(), graphic.canvas.additionalShaderChannels);

            var rect = transform as RectTransform;
            rect.GetLocalCorners(_localCorners);

            DivideAndEffect(this, _localCorners);

            _text._modified = _divider.Finish(_text._modifiedVerticies, _text._modifiedIndices);

            if (_text._modified)
            {
                TMP_Text text = _graphic as TMP_Text;
                BuildMesh(_text._mesh, _text._modifiedVerticies, _text._modifiedIndices, text.mesh.bounds);
            }

            UpdateCanvasMesh();

            Profiler.EndSample();
        }

        /// <summary>
        /// Assigns either the original tmpro mesh or the modified mesh to the canvas renderer based on needs
        /// </summary>
        private void UpdateCanvasMesh()
        {
            TMP_Text text = _graphic as TMP_Text;
            bool hasCharacters = text && text.textInfo != null && text.textInfo.characterCount > 0;
            _text._canvasRenderer?.SetMesh(
            _text._modified && hasCharacters ? 
            _text._mesh : hasCharacters ? text.mesh : null);
        }

        /// <summary>
        /// Fills the given mesh with the given verts, indicies and bounds
        /// </summary>
        private static void BuildMesh(Mesh mesh, List<UIVertex> vertices, List<ushort> indicies, Bounds bounds)
        {
            int vertexCount = vertices.Count;

            var verts = new NativeArray<Vertex>(vertexCount, Allocator.Temp);
            for (int j = 0; j < vertexCount; j++)
            {
                verts[j] = new Vertex()
                {
                    pos = vertices[j].position,
                    normal = vertices[j].normal,
                    color = vertices[j].color,
                    tangent = vertices[j].tangent,
                    uv0 = vertices[j].uv0,
                    uv1 = vertices[j].uv1
                };
            }

            mesh.SetVertexBufferParams(vertexCount, Vertex.Layout);
            mesh.SetVertexBufferData(verts, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices);

            mesh.SetIndexBufferParams(indicies.Count, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indicies, 0, 0, indicies.Count, MeshUpdateFlags.DontValidateIndices);

            SubMeshDescriptor subMeshDescriptor = new SubMeshDescriptor() { indexCount = indicies.Count };
            mesh.SetSubMesh(0, subMeshDescriptor);
            mesh.bounds = bounds;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 pos;
            public Vector3 normal;
            public Vector4 tangent;
            public Color32 color;
            public Vector4 uv0;
            public Vector4 uv1;

            public static readonly VertexAttributeDescriptor[] Layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            };
        }

        struct TMP
        {
            public CanvasRenderer _canvasRenderer;
            public Mesh _mesh;
            public bool _modified;
            public bool _isDirty;
            public List<UIVertex> _originalStream;
            public List<UIVertex> _modifiedVerticies;
            public List<ushort> _modifiedIndices;
            internal int lastUpdate;

            public void Init()
            {
                _originalStream = new List<UIVertex>();
                _modifiedVerticies = new List<UIVertex>();
                _modifiedIndices = new List<ushort>();
                _mesh = new Mesh();
                _mesh.subMeshCount = 1;
                _mesh.MarkDynamic();
            }
        }
#endif
    }
}