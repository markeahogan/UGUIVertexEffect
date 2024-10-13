using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PopupAsylum.UIEffects
{
    public class DesaturateUIEffect : UIEffect
    {
        [SerializeField, Range(0, 1)]
        float _desautration = 1;

        [SerializeField, HideInInspector]
        private Shader _uiEfffectShader;

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            if (_desautration == 0) return;

            if (IsUIEffectShader(graphicTransform))
            {
                vertex.uv1.x = _desautration;
            }
            else
            {
                vertex.color = Desaturate(vertex.color);
            }
        }

        private bool IsUIEffectShader(RectTransform graphicTransform)
        {
            return graphicTransform.GetComponent<Graphic>()?.material?.shader == _uiEfffectShader;
        }

        private Color32 Desaturate(Color32 color)
        {
            byte lunimance = (byte)(color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
            return Color32.Lerp(color, new Color32(lunimance, lunimance, lunimance, color.a), _desautration);
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            if (_desautration == 0) return;

            if (IsUIEffectShader(graphicTransform))
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var vert = verts[i];
                    vert.uv1.x = _desautration;
                    verts[i] = vert;
                }
            }
            else
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var vert = verts[i];
                    vert.color = Desaturate(vert.color);
                    verts[i] = vert;
                }
            }
        }
    }
}
