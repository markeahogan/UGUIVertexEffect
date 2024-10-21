using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// Modifies the verts of the attached graphic
    /// </summary>
    [ExecuteInEditMode]
    public sealed partial class UIMeshModifier : BaseMeshEffect, IMaterialModifier
    {
        public static readonly Dictionary<Type, UIDivider.Flags> DividerFlags = new Dictionary<Type, UIDivider.Flags>()
    {
        { typeof(Image), UIDivider.Flags.StartAsQuads },
        { typeof(RawImage), UIDivider.Flags.StartAsQuads },
#if SVG_INCLUDED
        { typeof(Unity.VectorGraphics.SVGImage), UIDivider.Flags.PreserveSorting },
#endif
#if TMPRO_INCLUDED
        { typeof(TMPro.TextMeshProUGUI), UIDivider.Flags.StartAsQuads },
#endif
        };

        private static Vector3[] _localCorners = new Vector3[4];
        private static List<Plane> _divisions = new List<Plane>();
        private static UIDivider _divider = new UIDivider();
        private static Material _defaultEffectMaterial;

        [HideInInspector, SerializeField]
        private Shader _effectShader;

        [HideInInspector, NonSerialized]
        public UIMeshModifier ParentRef;

        private List<UIEffect> _effects = new List<UIEffect>();
        private List<UIMeshModifier> _children = new List<UIMeshModifier>();
        private List<IEffectFilter> _filters = new List<IEffectFilter>();

        private int _childCount = 0;

        private bool _effectsAreGraphicSpace = true;
        private bool _effectsModifyPosition = false;
        private bool _effectsUseShader = false;

        private bool _isGraphic = false;
        private UIDivider.Flags? _hintFlags = null;
        private Graphic _graphic;

        private bool EffectsModifyPosition => _effectsModifyPosition || (ParentRef != null && ParentRef.EffectsModifyPosition);
        private bool MarkAsDirtyIfTransformChanges => !_effectsAreGraphicSpace || (ParentRef != null && ParentRef.MarkAsDirtyIfTransformChanges);
        private bool EffectsUseShader => _effectsUseShader || (ParentRef != null && ParentRef.EffectsUseShader);

        protected override void OnEnable()
        {
            if (!_effectShader) _effectShader = Shader.Find("UGUIVertexEffect");
            if (_effectShader && !_defaultEffectMaterial)
            {
                _defaultEffectMaterial = new Material(_effectShader);
                _defaultEffectMaterial.hideFlags = HideFlags.DontSave;
            }

            Repair(this);
            SetupChildren(Children.All);
            SetTextCallbacksRegistered(true);
            UpdateParent();

            if (ParentRef == null) UpdateGraphicRaycasterCallback();
        }

        private void OnTransformChildrenChanged() => SetupChildren(Children.Newest);

        /// <summary>
        /// UIEffects can displace the verts of the graphic making it hard to select in the scene view
        /// This draws an invisible gizmo so scene picking works as expected
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_isGraphic) { return; }

            GetWorldSpaceCorners(_corners);
            var raycastPose = PoseFromCorners(_corners, (transform as RectTransform).pivot);
            var matrixAfter = Matrix4x4.TRS(raycastPose.position, raycastPose.rotation, Vector3.one).inverse;
            var sizeAfter1 = matrixAfter.MultiplyPoint(_corners[2]) - matrixAfter.MultiplyPoint(_corners[0]);
            var sizeAfter2 = matrixAfter.MultiplyPoint(_corners[1]) - matrixAfter.MultiplyPoint(_corners[3]);
            var sizeAfter = Vector3.Max(sizeAfter1, sizeAfter2);

            Gizmos.color = Color.clear;
            Gizmos.matrix = matrixAfter.inverse;
            Gizmos.DrawCube(Vector3.zero, new Vector3(sizeAfter.x, sizeAfter.y, 0.001f));
        }

        protected override void OnDisable()
        {
            ClearParent();
            SetTextCallbacksRegistered(false);
        }

        private void LateUpdate()
        {
            if (transform.hasChanged && MarkAsDirtyIfTransformChanges)
            {
                MarkAsDirty();
            }
        }


        private void ClearParent()
        {
            if (ParentRef != null)
            {
                ParentRef.IncludeChild(this, false);
                ParentRef = null;
            }
        }

        private void UpdateParent()
        {
            var newParent = isActiveAndEnabled && transform.parent ? transform.parent.GetComponent<UIMeshModifier>() : null;

            if (newParent == ParentRef) return;

            ClearParent();

            ParentRef = newParent;

            if (ParentRef != null)
            {
                ParentRef.IncludeChild(this, true);
            }

            UpdateGraphicRaycasterCallback();
            SetGraphicDirty();
        }

        private void IncludeChild(UIMeshModifier child, bool include)
        {
            if (include) _children.Add(child);
            else _children.Remove(child);
        }

        private void SetupChildren(Children mode)
        {
            if (_childCount < transform.childCount)
            {
                int newestChild = transform.childCount - 1;
                int end = mode == Children.All ? 0 : newestChild;
                for (int i = newestChild; i >= end; i--)
                {
                    var child = GetOrAdd(transform.GetChild(i));

                    // if the child has its own UIEffect it may have already called OnEnable but will not have found this parent
                    // call UpdateParent so it will find this, no need to call SetupChildren since it will have already done so in OnEnable
                    if (_children.Count == 0 || _children[_children.Count - 1] != child)
                    {
                        child.UpdateParent();
                    }
                }
            }
            _childCount = transform.childCount;
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            if (!_isGraphic) Repair(this);
            if (!_graphic.canvas) return;

            UIDivider.Flags dividerFlags;
            if (_hintFlags.HasValue) dividerFlags = _hintFlags.Value;
            else DividerFlags.TryGetValue(_graphic.GetType(), out dividerFlags);

            _divider.Start(vh, dividerFlags, _graphic.canvas.additionalShaderChannels);

            var rect = transform as RectTransform;
            rect.GetLocalCorners(_localCorners);

            DivideAndEffect(this, _localCorners);

            _divider.Finish();
        }

        private void DivideAndEffect(UIMeshModifier meshEffector, Vector3[] localCorners)
        {
            var graphicTransform = meshEffector.transform as RectTransform;

            for (int i = 0; i < _effects.Count; i++)
            {
                UIEffect effect = _effects[i];

                if (!effect.PreProcess(meshEffector)) continue;

                effect.InternalAddClippingPlanes(meshEffector, _divisions);

                if (_divisions.Count > 0)
                {
                    _divider.Clip(_divisions, localCorners);
                    _divisions.Clear();
                }

                effect.InternalAddDivisions(meshEffector, _divisions);

                if (_divisions.Count > 0)
                {
                    _divider.Divide(_divisions, localCorners);
                    _divisions.Clear();
                }

                if (effect.AffectsVertex)
                {
                    /*
                    _divider.ForEach(vertex =>
                    {
                        effect.InternalModifyVertex(graphicTransform, ref vertex);
                        return vertex;
                    });
                    */

                    effect.InternalModifyVertices(graphicTransform, _divider.GetVertices());

                    if (effect.AffectsPosition)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            UIVertex vert = new UIVertex() { position = localCorners[j] };
                            effect.InternalModifyVertex(graphicTransform, ref vert);
                            localCorners[i] = vert.position;
                        }
                    }
                }
            }

            if (ParentRef != null)
            {
                ParentRef.DivideAndEffect(meshEffector, localCorners);
            }
        }

        private UIVertex GetFinalPosition(UIMeshModifier meshEffector, ref UIVertex vertex)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                UIEffect effect = _effects[i];
                if (effect.AffectsVertex && effect.AffectsPosition && effect.PreProcess(meshEffector))
                {
                    effect.InternalModifyVertex(meshEffector.transform as RectTransform, ref vertex);
                }
            }

            if (ParentRef != null)
            {
                ParentRef.GetFinalPosition(meshEffector, ref vertex);
            }

            return vertex;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            UpdateParent();
        }

        private void SetGraphicDirty()
        {
            if (_isGraphic)
            {
#if UNITY_EDITOR
                if (_graphic == null)
                {
                    var exception = new NullReferenceException("The Graphic component for this instance has been destroyed, " +
                                        "call UGUIMeshEffector.Repair(gameObject) to fix this error, or disable/enable the gameobject");
                    Debug.LogException(exception, this);
                    return;
                }
#endif

                if (_graphic.canvas && !SetTextDirty())
                {
                    _graphic.SetVerticesDirty();
                    if (EffectsUseShader) _graphic.SetMaterialDirty();
                }
            }
        }

        internal void SetEffectAdded(UIEffect effect, bool add)
        {
            _effects.Remove(effect);
            if (add) _effects.Add(effect);

            _effectsAreGraphicSpace = _effects.TrueForAll(x => x.UIVertexSpace == UIEffect.Space.Graphic);
            _effectsModifyPosition = !_effects.TrueForAll(x => !x.AffectsPosition);
            _effectsUseShader = !_effects.TrueForAll(x => !x.UsesShader);

            MarkAsDirty();
        }

        internal static UIMeshModifier GetOrAdd(Component source)
        {
            if (!source.TryGetComponent(out UIMeshModifier result))
            {
                result = source.gameObject.AddComponent<UIMeshModifier>();
                if (result)// result can be null in prefabs
                {
                    result.hideFlags = HideFlags.DontSave;// | HideFlags.HideInInspector;
                }
            }
            return result;
        }

        /// <summary>
        /// Call this after removing/replacing a UIMeshModifier's Graphic component e.g. Changing an Image to a RawImage
        /// </summary>
        public static void Repair(GameObject gameObject)
        {
            if (gameObject.TryGetComponent(out UIMeshModifier result))
            {
                Repair(result);
                if (result._isGraphic) result._graphic.SetVerticesDirty();
            }
        }

        /// <summary>
        /// Call this after removing/replacing a UIMeshModifier's Graphic component e.g. Changing an Image to a RawImage
        /// </summary>
        private static void Repair(UIMeshModifier effector)
        {
            effector._isGraphic = effector.TryGetComponent(out effector._graphic);
            if (effector.TryGetComponent<UIDividerHints>(out var hints) && hints.enabled)
            {
                effector._hintFlags = hints.flags;
            }
            else
            {
                effector._hintFlags = null;
            }
            effector.GetComponents(effector._filters);
        }

        internal void MarkAsDirty()
        {
            transform.hasChanged = false;
            if (isActiveAndEnabled)
            {
                SetGraphicDirty();
            }
            _children.ForEach(child => child.MarkAsDirty());
        }

        public Material GetModifiedMaterial(Material material)
        {
            Material defaultMaterial = _graphic.defaultMaterial;

            if (!UsesUGUIEffectShader())
            {
                if (material.shader == _effectShader) material.shader = defaultMaterial.shader;
                return material;
            }

            // graphic is not using any material customization, 
            if (material == defaultMaterial) return _defaultEffectMaterial;

            // graphic is using a custom material, but its the default shader
            // its probably Masked, replace the shader with the effect shader
            if (material.shader == defaultMaterial.shader) material.shader = _effectShader;

            return material;
        }

        public bool UsesUGUIEffectShader()
        {
            if (!_isGraphic) return false;
            if (!isActiveAndEnabled) return false;
            if (!EffectsUseShader) return false;
#if TMPRO_INCLUDED 
            if (_graphic is TMPro.TextMeshProUGUI) return false;
#endif
            if (_graphic.material != _graphic.defaultMaterial) return false;
            return true;
        }

        enum Children
        {
            All,
            Newest
        }
    }

    interface IEffectFilter
    {
        bool PassesFilter(UIEffect effect);
    }
}