using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using static System.MathF;

using mplt = Mediapipe.LocationData.Types;

namespace Mediapipe.Unity
{
    public static class TransformExtension
    {
        // Recursively find a child by name
        public static Transform FindDeepChild(this Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            foreach (Transform t in parent)
            {
                child = t.FindDeepChild(name);
                if (child != null)
                    return child;
            }

            return null;
        }

        // Find ALL children with the given name recursively
        public static void FindAllDeepChildren(this Transform parent, string name, List<Transform> results)
        {
            foreach (Transform t in parent)
            {
                if (t.name == name)
                    results.Add(t);
                
                t.FindAllDeepChildren(name, results);
            }
        }
    }

    public class PointListAnnotation : ListAnnotation<PointAnnotation>
    {
        private static PointListAnnotation s_runtimeTunerOwner;
        private const float ScaleMultiplierMin = 0.30f;
        private const float ScaleMultiplierMax = 8.00f;

        public List<Transform> rightupperArmObjs = new List<Transform>();
        public List<Transform> leftupperArmObjs = new List<Transform>();
        public List<Transform> rightlowerArmObjs = new List<Transform>();
        public List<Transform> leftlowerArmObjs = new List<Transform>();
        
        [SerializeField] private GameObject _targetObject;
        [SerializeField] private UnityEngine.Color _color = UnityEngine.Color.green;
        [SerializeField] private float _radius = 15.0f;
        [SerializeField] private float _scaleMultiplier = 1.5f; 
        [SerializeField] private float _modelRotationOffset = 180.0f;
        [SerializeField] private bool _mirrorMode = true;
        [SerializeField] private bool _useDynamicRotation = false;
        [Header("Model Normalization")]
        [SerializeField] private bool _normalizeModelOnSet = true;
        [SerializeField] private float _targetModelHeight = 1.6f;
        [SerializeField] private float _normalizeMinHeight = 0.3f;
        [SerializeField] private float _normalizeMaxHeight = 5.0f;
        [SerializeField] private float _normalizeMaxCenterDistance = 10.0f;
        [SerializeField] private bool _forceVisibleInFrontOfCamera = true;
        [SerializeField] private float _fallbackDepthMeters = 2.5f;
        [SerializeField] private float _minimumModelScale = 0.6f;
        [Header("Pose Anchoring")]
        [SerializeField] [Range(0f, 1f)] private float _modelAnchorHeight01 = 0.84f;
        [SerializeField] [Range(0f, 1f)] private float _poseAnchorToShoulder01 = 0.0f;
        [SerializeField] private float _poseAnchorDownOffsetFactor = 1.10f;
        [Header("Auto Follow")]
        [SerializeField] private bool _autoFollowFromLandmarks = true;
        [SerializeField] [Range(0f, 1.5f)] private float _faceFollowInfluence = 0.35f;
        [SerializeField] private float _horizontalOffset01 = 0.0f;
        [SerializeField] private bool _continuousAutoScale = true;
        [SerializeField] private float _autoScaleLerpSpeed = 12.0f;
        [SerializeField] private float _depthLerpSpeed = 10.0f;
        [Header("Runtime Tuner (wear_to_3d)")]
        [SerializeField] private bool _enableRuntimeTuner = true;
        [SerializeField] private bool _runtimeTunerOnlyInWearScene = true;
        [SerializeField] private bool _runtimeTunerCollapsed = false;
        [SerializeField] private bool _runtimeTunerHidden = false;
        [SerializeField] private int _runtimeTunerTabIndex = 0;
        [SerializeField] private float _runtimeTunerUiScale = 1.60f;

        [Header("Visibility Stability")]
        [SerializeField] private int _visibilityBufferFrames = 10; // Number of frames to wait before hiding model
        [SerializeField] [Range(0f, 1f)] private float _bodyVisibilityThreshold = 0.35f;
        [SerializeField] private bool _userClothVisible = true;
        private int _visibilityCounter = 0;
        private bool _loggedVisibilityFallback = false;
        private bool _loggedInsufficientLandmarks = false;
        private bool _hasBodyTracking = false;

        private static readonly int[] _requiredBodyLandmarkIndices = { 0, 11, 12 };

        private Vector3 _lastForward = Vector3.forward;
        private bool _isScaled = false;
        private Vector3 _modelBaseOffset;
        private float _smoothedDepthMeters = -1.0f;

        private Vector3 MPToUnity(Vector3 mp)
        {
            return new Vector3(mp.x, mp.y, -mp.z);
        }

        private void CenterModel(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds localBounds = new Bounds();
            bool initialized = false;

            foreach (var r in renderers)
            {
                Bounds b = TransformBoundsToLocal(r.bounds, model.transform);
                if (!initialized)
                {
                    localBounds = b;
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(b);
                }
            }

            Vector3 offset = model.transform.position - model.transform.TransformPoint(localBounds.center);
            model.transform.position += offset;
        }

        private Bounds TransformBoundsToLocal(Bounds worldBounds, Transform target)
        {
            var center = target.InverseTransformPoint(worldBounds.center);
            var extents = worldBounds.extents;
            return new Bounds(center, extents * 2f);
        }

        void PrintBones(Transform root, string indent = "")
        {
            Debug.Log(indent + root.name);
            foreach (Transform child in root)
            {
                PrintBones(child, indent + "      ");
            }
        }

        private void BindBones(GameObject model)
        {
            Debug.Log($"Binding bones for: {model.name}");
            
            rightupperArmObjs.Clear();
            leftupperArmObjs.Clear();
            rightlowerArmObjs.Clear();
            leftlowerArmObjs.Clear();

            string leftPrefix = _mirrorMode ? "R_" : "L_";
            string rightPrefix = _mirrorMode ? "L_" : "R_";
            string leftArmName = _mirrorMode ? "R_Arm" : "L_Arm";
            string rightArmName = _mirrorMode ? "L_Arm" : "R_Arm";

            model.transform.FindAllDeepChildren(leftPrefix + "UpperArm", leftupperArmObjs);
            model.transform.FindAllDeepChildren(leftArmName, leftlowerArmObjs);
            model.transform.FindAllDeepChildren(rightPrefix + "UpperArm", rightupperArmObjs);
            model.transform.FindAllDeepChildren(rightArmName, rightlowerArmObjs);

            Debug.Log($"Bones Found - Mirror Mode: {_mirrorMode}");
            Debug.Log($"  L_UpperArm: {leftupperArmObjs.Count}, L_LowerArm: {leftlowerArmObjs.Count}");
            Debug.Log($"  R_UpperArm: {rightupperArmObjs.Count}, R_LowerArm: {rightlowerArmObjs.Count}");
        }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        private void ApplyRuntimeAnchorPreset()
        {
            _modelAnchorHeight01 = Mathf.Clamp01(_modelAnchorHeight01);
            _poseAnchorToShoulder01 = Mathf.Clamp01(_poseAnchorToShoulder01);
            _poseAnchorDownOffsetFactor = Mathf.Clamp(_poseAnchorDownOffsetFactor, -2.0f, 2.0f);
            _scaleMultiplier = Mathf.Clamp(_scaleMultiplier, ScaleMultiplierMin, ScaleMultiplierMax);
            _faceFollowInfluence = Mathf.Clamp(_faceFollowInfluence, 0.0f, 1.5f);
            _horizontalOffset01 = Mathf.Clamp(_horizontalOffset01, -0.5f, 0.5f);
            _autoScaleLerpSpeed = Mathf.Clamp(_autoScaleLerpSpeed, 1.0f, 30.0f);
            _depthLerpSpeed = Mathf.Clamp(_depthLerpSpeed, 1.0f, 30.0f);
        }

        private bool IsWearTo3DScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return sceneName == "wear_to_3d" || sceneName == "wear_to_3D";
        }

        private static bool DrawRuntimeSlider(string label, ref float value, float min, float max, float step)
        {
            float before = value;
            GUILayout.Label(label + ": " + value.ToString("F2"), GUILayout.Height(30f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(56f), GUILayout.Height(34f)))
            {
                value -= step;
            }
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.MinWidth(270f), GUILayout.Height(34f));
            if (GUILayout.Button("+", GUILayout.Width(56f), GUILayout.Height(34f)))
            {
                value += step;
            }
            GUILayout.EndHorizontal();

            value = Mathf.Clamp(value, min, max);
            if (step > 0f)
            {
                value = Mathf.Round(value / step) * step;
            }
            return !Mathf.Approximately(before, value);
        }

        private bool DrawRuntimeTabButton(int tabIndex, string label)
        {
            bool selected = _runtimeTunerTabIndex == tabIndex;
            string text = selected ? "● " + label : label;
            if (GUILayout.Button(text, GUILayout.Height(38f)))
            {
                _runtimeTunerTabIndex = tabIndex;
                return true;
            }
            return false;
        }

        private void ApplyScaleMultiplierImmediate(float oldScaleMultiplier)
        {
            if (_targetObject == null) return;
            if (oldScaleMultiplier <= 1e-6f) return;

            float ratio = _scaleMultiplier / oldScaleMultiplier;
            if (!IsFinite(ratio) || ratio <= 0f) return;
            _targetObject.transform.localScale *= ratio;
        }

        private void MarkRuntimeTuningChanged()
        {
            ApplyRuntimeAnchorPreset();
            _isScaled = false;
            _loggedVisibilityFallback = false;
        }

        private bool IsRuntimeTunerOwner()
        {
            if (s_runtimeTunerOwner == null || !s_runtimeTunerOwner.isActiveAndEnabled)
            {
                s_runtimeTunerOwner = this;
            }
            return s_runtimeTunerOwner == this;
        }

        private void OnDisable()
        {
            if (s_runtimeTunerOwner == this)
            {
                s_runtimeTunerOwner = null;
            }
        }

        private void OnDestroy()
        {
            if (s_runtimeTunerOwner == this)
            {
                s_runtimeTunerOwner = null;
            }
        }

        private void OnGUI()
        {
            if (!_enableRuntimeTuner || !Application.isPlaying)
            {
                return;
            }

            if (_runtimeTunerOnlyInWearScene && !IsWearTo3DScene())
            {
                return;
            }

            if (!IsRuntimeTunerOwner())
            {
                return;
            }

            float uiScale = Mathf.Clamp(_runtimeTunerUiScale, 0.60f, 3.00f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

            float x = 16f / uiScale;
            float y = 16f / uiScale;
            if (_runtimeTunerHidden)
            {
                UnityEngine.Rect openRect = default;
                openRect.x = x;
                openRect.y = y;
                openRect.width = 240f;
                openRect.height = 100f;
                GUILayout.BeginArea(openRect, GUI.skin.box);
                GUILayout.Label("着用補正UI");
                if (GUILayout.Button("開く", GUILayout.Height(42f)))
                {
                    _runtimeTunerHidden = false;
                }
                GUILayout.EndArea();
                GUI.matrix = oldMatrix;
                return;
            }

            float width = 520f;
            float height = _runtimeTunerCollapsed ? 120f : 640f;
            UnityEngine.Rect areaRect = default;
            areaRect.x = x;
            areaRect.y = y;
            areaRect.width = width;
            areaRect.height = height;
            GUILayout.BeginArea(areaRect, GUI.skin.box);
            GUILayout.Label("着用補正ランタイムチューナー");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_runtimeTunerCollapsed ? "詳細を開く" : "詳細を閉じる", GUILayout.Height(40f)))
            {
                _runtimeTunerCollapsed = !_runtimeTunerCollapsed;
            }
            if (GUILayout.Button("初期化", GUILayout.Height(40f)))
            {
                _modelAnchorHeight01 = 0.84f;
                _poseAnchorToShoulder01 = 0.00f;
                _poseAnchorDownOffsetFactor = 1.10f;
                _scaleMultiplier = 1.70f;
                _faceFollowInfluence = 0.35f;
                _horizontalOffset01 = 0.0f;
                _runtimeTunerUiScale = 1.60f;
                MarkRuntimeTuningChanged();
            }
            if (GUILayout.Button("完全に閉じる", GUILayout.Height(40f)))
            {
                _runtimeTunerHidden = true;
            }
            GUILayout.EndHorizontal();

            if (!_runtimeTunerCollapsed)
            {
                bool changed = false;
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                DrawRuntimeTabButton(0, "位置補正");
                DrawRuntimeTabButton(1, "サイズ補正");
                DrawRuntimeTabButton(2, "補正の詳細");
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);

                if (_runtimeTunerTabIndex == 0)
                {
                    changed |= DrawRuntimeSlider("モデル基準高さ", ref _modelAnchorHeight01, 0.00f, 1.00f, 0.01f);
                    changed |= DrawRuntimeSlider("肩への寄せ量", ref _poseAnchorToShoulder01, 0.00f, 1.00f, 0.01f);
                    changed |= DrawRuntimeSlider("下方向オフセット", ref _poseAnchorDownOffsetFactor, -2.00f, 2.00f, 0.02f);
                    changed |= DrawRuntimeSlider("横追従量", ref _faceFollowInfluence, 0.00f, 1.50f, 0.01f);
                    changed |= DrawRuntimeSlider("横オフセット", ref _horizontalOffset01, -0.50f, 0.50f, 0.01f);
                    GUILayout.Space(4f);
                    GUILayout.Label("目安: 高さ/下方向オフセットを上げるとモデルが下に移動します。");
                }
                else if (_runtimeTunerTabIndex == 1)
                {
                    float oldScaleMultiplier = _scaleMultiplier;
                    changed |= DrawRuntimeSlider("モデル拡大率", ref _scaleMultiplier, ScaleMultiplierMin, ScaleMultiplierMax, 0.01f);
                    if (!Mathf.Approximately(oldScaleMultiplier, _scaleMultiplier))
                    {
                        ApplyScaleMultiplierImmediate(oldScaleMultiplier);
                    }
                    changed |= DrawRuntimeSlider("UIサイズ", ref _runtimeTunerUiScale, 0.60f, 3.00f, 0.05f);
                }
                else
                {
                    GUILayout.Label("現在の補正値");
                    GUILayout.Label($"・モデル基準高さ: {_modelAnchorHeight01:F2}");
                    GUILayout.Label($"・肩への寄せ量: {_poseAnchorToShoulder01:F2}");
                    GUILayout.Label($"・下方向オフセット: {_poseAnchorDownOffsetFactor:F2}");
                    GUILayout.Label($"・横追従量: {_faceFollowInfluence:F2}");
                    GUILayout.Label($"・横オフセット: {_horizontalOffset01:F2}");
                    GUILayout.Label($"・モデル拡大率: {_scaleMultiplier:F2}");
                    GUILayout.Label($"・UIサイズ: {_runtimeTunerUiScale:F2}");
                    GUILayout.Space(8f);
                    GUILayout.Label("使い方");
                    GUILayout.Label("1) 位置補正タブで襟の高さを合わせる");
                    GUILayout.Label("2) サイズ補正タブで服の大きさを合わせる");
                    GUILayout.Label("3) 合ったら詳細を閉じるで最小表示にする");
                }

                if (changed)
                {
                    MarkRuntimeTuningChanged();
                }
            }

            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        public void SetModel(GameObject newModel)
        {
            if (newModel == null) return;

            ApplyRuntimeAnchorPreset();

            if (_targetObject != null)
                Destroy(_targetObject);

            _targetObject = Instantiate(newModel, transform);
            _targetObject.name = "ActivePoseModel";
            
            // Fix layers to match annotation
            SetLayerRecursively(_targetObject, gameObject.layer);
            
            // Fix Frustum Culling issues (model disappearing when rotated)
            foreach (var smr in _targetObject.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
            }
            foreach (var mr in _targetObject.GetComponentsInChildren<MeshRenderer>())
            {
                // For static meshes, ensure bounds are large enough or they don't cull
                // Usually updateWhenOffscreen is for Skinned, but for regular MeshRenderer
                // we ensure it's activated.
            }

            // Disable Animator to prevent it fighting with our manual bone posing
            var animator = _targetObject.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            
            _targetObject.SetActive(true);
            _isScaled = false;
            _loggedVisibilityFallback = false;
            _loggedInsufficientLandmarks = false;
            _smoothedDepthMeters = -1.0f;

            BindBones(_targetObject);
            CenterModel(_targetObject);
            NormalizeModelOnSet(_targetObject);

            _targetObject.transform.rotation = Quaternion.Euler(0, _modelRotationOffset, 0);
            _modelBaseOffset = _targetObject.transform.position;
            
            PlaceModelAtScreenCenter();
            _visibilityCounter = 0;
            _hasBodyTracking = false;
            if (_userClothVisible)
            {
                SetActive(true);
                ApplyCameraFallbackPose();
            }
            else
            {
                SetActive(false);
            }
            Debug.Log(
                "[PointListAnnotation] Anchor tuning: " +
                $"modelAnchorHeight01={_modelAnchorHeight01:F2}, " +
                $"poseAnchorToShoulder01={_poseAnchorToShoulder01:F2}, " +
                $"poseAnchorDownOffsetFactor={_poseAnchorDownOffsetFactor:F2}, " +
                $"scaleMultiplier={_scaleMultiplier:F2}");
            LogModelRenderDiagnostics(_targetObject);
        }

        public void Draw(IList<NormalizedLandmark> targets, bool visualizeZ = true)
        {
            if (!_userClothVisible)
            {
                SetActive(false);
                return;
            }

            _hasBodyTracking = targets != null && targets.Count > 0;
            SetActive(true);

            if (_hasBodyTracking)
            {
                _visibilityCounter = _visibilityBufferFrames;

                CallActionForAll(targets, (annotation, target) =>
                {
                    annotation?.Draw(target, visualizeZ);
                });
            }
        }

        private void LateUpdate()
        {
            if (!_userClothVisible)
            {
                if (isActive)
                {
                    SetActive(false);
                }
                return;
            }

            if (isActive && _targetObject != null)
            {
                if (_hasBodyTracking)
                {
                    UpdateModelPose();
                }
                else
                {
                    ApplyCameraFallbackPose();
                }
            }
        }

        private void UpdateModelPose()
        {
            if (_targetObject == null)
                return;

            if (children.Count < 25)
            {
                ApplyCameraFallbackPose();
                return;
            }

            WearRightUpperArm();
            WearLeftUpperArm();
            WearRightlowerArm();
            WearLeftlowerArm();

            UpdateModelTransformFromChildren();
        }

        private void AlignModelToBottomCenter(GameObject model, Vector3 bottomCenter)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var rend in renderers)
                bounds.Encapsulate(rend.bounds);

            Vector3 currentBottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 delta = bottomCenter - currentBottomCenter;
            model.transform.position += delta;
        }

        private void AlignModelAnchorToWorldPoint(GameObject model, Vector3 worldPoint, float modelAnchorHeight01)
        {
            if (model == null)
            {
                return;
            }

            if (!TryGetRendererBounds(model, out Bounds bounds))
            {
                return;
            }

            float t = Mathf.Clamp01(modelAnchorHeight01);
            Vector3 currentAnchor = new Vector3(
                bounds.center.x,
                bounds.min.y + (bounds.size.y * t),
                bounds.center.z);
            Vector3 delta = worldPoint - currentAnchor;
            model.transform.position += delta;
        }

        private bool TryGetBodySize(out float shoulderWidth, out float bodyHeight)
        {
            shoulderWidth = 0f;
            bodyHeight = 0f;

            if (children.Count <= 24) return false;

            Vector3 upLeft = children[11].transform.position;
            Vector3 upRight = children[12].transform.position;
            Vector3 downLeft = children[23].transform.position;
            Vector3 downRight = children[24].transform.position;

            shoulderWidth = Vector3.Distance(upLeft, upRight);

            Vector3 topCenter = (upLeft + upRight) / 2f;
            Vector3 bottomCenter = (downLeft + downRight) / 2f;
            bodyHeight = Vector3.Distance(topCenter, bottomCenter);

            return shoulderWidth > 0.001f && bodyHeight > 0.001f;
        }

        private Vector3 GetModelOriginalSize(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return Vector3.one;

            Bounds bounds = renderers[0].bounds;
            foreach (var rend in renderers)
                bounds.Encapsulate(rend.bounds);

            return bounds.size;
        }

        private void FitModelScale(GameObject model, float targetWidth, float targetHeight, bool smooth)
        {
            if (!IsFinite(targetWidth) || !IsFinite(targetHeight))
            {
                return;
            }

            targetWidth = Mathf.Abs(targetWidth);
            targetHeight = Mathf.Abs(targetHeight);
            if (targetWidth < 1e-4f || targetHeight < 1e-4f)
            {
                return;
            }

            Vector3 orig = GetModelOriginalSize(model);
            if (orig.x < 1e-5f || orig.y < 1e-5f)
            {
                return;
            }

            float scaleX = targetWidth / orig.x;
            float scaleY = targetHeight / orig.y;
            float scaleZ = (orig.z > 0.001f) ? scaleY * (orig.z / orig.y) : 1f;

            float ratio = (scaleX + scaleY + scaleZ) / 3f;
            float currentScale = Mathf.Max(0.0001f, model.transform.localScale.x);
            float targetScale = Mathf.Clamp(currentScale * ratio * _scaleMultiplier, 0.01f, 500f);
            if (!IsFinite(targetScale))
            {
                return;
            }

            if (!smooth)
            {
                model.transform.localScale = Vector3.one * targetScale;
                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(1f, _autoScaleLerpSpeed) * Time.deltaTime);
            model.transform.localScale = Vector3.one * Mathf.Lerp(currentScale, targetScale, t);
        }

        public void SetColor(UnityEngine.Color color)
        {
            _color = color;
            ApplyColor(_color);
        }

        public void SetRadius(float radius)
        {
            _radius = radius;
            ApplyRadius(_radius);
        }

        public bool IsClothVisible()
        {
            return _userClothVisible;
        }

        public void SetClothVisible(bool visible)
        {
            _userClothVisible = visible;
            if (!visible)
            {
                _hasBodyTracking = false;
                SetActive(false);
            }
            else if (_targetObject != null)
            {
                SetActive(true);
                ApplyCameraFallbackPose();
            }
        }

        public void ToggleClothVisible()
        {
            SetClothVisible(!_userClothVisible);
        }

        public void Draw(IList<Vector3> targets)
        {
            if (!_userClothVisible)
            {
                SetActive(false);
                return;
            }

            // Similar buffering for Vector3 mode if needed
            if (targets != null && targets.Count > 0)
            {
                _visibilityCounter = _visibilityBufferFrames;
                SetActive(true);
                CallActionForAll(targets, (annotation, target) =>
                {
                    annotation?.Draw(target);
                });
            }
            else
            {
                SetActive(true);
            }
        }

        public void Draw(IList<Landmark> targets, Vector3 scale, bool visualizeZ = true)
        {
            if (!_userClothVisible)
            {
                SetActive(false);
                return;
            }

            if (targets != null && targets.Count > 0)
            {
                _visibilityCounter = _visibilityBufferFrames;
                SetActive(true);
                CallActionForAll(targets, (annotation, target) =>
                {
                    annotation?.Draw(target, scale, visualizeZ);
                });

                UpdateModelPose(); // Only update pose when tracking is active
            }
            else
            {
                SetActive(true);
            }
        }

        public void Draw(LandmarkList targets, Vector3 scale, bool visualizeZ = true)
        {
            Draw(targets.Landmark, scale, visualizeZ);
        }

        public void Draw(NormalizedLandmarkList targets, bool visualizeZ = true)
        {
            Draw(targets.Landmark, visualizeZ);
        }

        public void Draw(IList<mplt.RelativeKeypoint> targets, float threshold = 0.0f)
        {
            if (!_userClothVisible)
            {
                SetActive(false);
                return;
            }

            if (targets != null && targets.Count > 0)
            {
                _visibilityCounter = _visibilityBufferFrames;
                SetActive(true);
                CallActionForAll(targets, (annotation, target) =>
                {
                    annotation?.Draw(target, threshold);
                });
            }
            else
            {
                SetActive(true);
            }
        }

        public void Draw(IList<Landmark> worldLandmarks)
        {
            if (!_userClothVisible)
            {
                SetActive(false);
                return;
            }

            if (worldLandmarks != null && worldLandmarks.Count > 0)
            {
                _visibilityCounter = _visibilityBufferFrames;
                SetActive(true);
                for (int i = 0; i < children.Count && i < worldLandmarks.Count; i++)
                {
                    var lm = worldLandmarks[i];
                    children[i].transform.localPosition = new Vector3(lm.X, lm.Y, -lm.Z);
                }
            }
            else
            {
                SetActive(true);
            }
        }

        private void UpdateModelTransformFromChildren()
        {
            if (_targetObject == null || children.Count < 25)
                return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                ApplyCameraFallbackPose();
                return;
            }

            Vector3 rightShoulderLocal = children[12].transform.localPosition;
            Vector3 leftShoulderLocal = children[11].transform.localPosition;
            Vector3 rightHipLocal = children[24].transform.localPosition;
            Vector3 leftHipLocal = children[23].transform.localPosition;

            Vector3 shoulderCenterLocal = (leftShoulderLocal + rightShoulderLocal) * 0.5f;
            Vector3 hipCenterLocal = (leftHipLocal + rightHipLocal) * 0.5f;
            Vector3 shoulderToHipLocal = shoulderCenterLocal - hipCenterLocal;
            float torsoHeightLocal = shoulderToHipLocal.magnitude;
            Vector3 bodyUpLocal = torsoHeightLocal > 1e-5f ? shoulderToHipLocal / torsoHeightLocal : Vector3.up;

            float yaw = 0f;
            if (_useDynamicRotation)
            {
                Vector3 bodyRight = (rightShoulderLocal - leftShoulderLocal).normalized;
                Vector3 bodyForward = Vector3.Cross(bodyRight, bodyUpLocal).normalized;
                Vector3 camUp = cam.transform.up;
                Vector3 camForward = cam.transform.forward;
                Vector3 projectedForward = Vector3.ProjectOnPlane(bodyForward, camUp).normalized;
                yaw = Vector3.SignedAngle(camForward, projectedForward, camUp);
            }

            Quaternion targetRot = Quaternion.Euler(0f, yaw + _modelRotationOffset, 0f);
            _targetObject.transform.rotation = Quaternion.Slerp(_targetObject.transform.rotation, targetRot, Time.deltaTime * 10f);

            Vector2 anchorViewport = new Vector2(0.5f, 0.5f);
            if (_autoFollowFromLandmarks && children.Count > 0)
            {
                Vector3 noseLocal = children[0].transform.localPosition;
                if (TryLocalPointToViewport(noseLocal, out Vector2 noseViewport))
                {
                    Vector2 faceOffset = noseViewport - new Vector2(0.5f, 0.5f);
                    anchorViewport += faceOffset * _faceFollowInfluence;
                }
            }
            anchorViewport.x += _horizontalOffset01;
            anchorViewport.x = Mathf.Clamp01(anchorViewport.x);
            anchorViewport.y = Mathf.Clamp01(anchorViewport.y);

            float targetDepth = ComputeDepthFromTorsoHeight(torsoHeightLocal, cam);
            float depth = SmoothDepthMeters(targetDepth);
            depth = Mathf.Max(cam.nearClipPlane + 0.3f, depth);

            if (TryGetBodySizeAtDepth(cam, leftShoulderLocal, rightShoulderLocal, leftHipLocal, rightHipLocal, depth, out float shoulderWidthWorld, out float bodyHeightWorld))
            {
                bool smoothScale = _continuousAutoScale || _isScaled;
                FitModelScale(_targetObject, shoulderWidthWorld, bodyHeightWorld, smoothScale);
                _isScaled = true;
                _modelBaseOffset = hipCenterLocal;
            }

            Vector3 poseAnchor = cam.ViewportToWorldPoint(new Vector3(anchorViewport.x, anchorViewport.y, depth));
            if (_forceVisibleInFrontOfCamera)
            {
                poseAnchor = EnsurePointInFrontOfCamera(poseAnchor);
            }

            _targetObject.transform.position = poseAnchor;
            AlignModelAnchorToWorldPoint(_targetObject, poseAnchor, _modelAnchorHeight01);
            EnsureMinimumScale(_targetObject);
        }

        private void ApplyCameraFallbackPose()
        {
            Camera cam = Camera.main;
            if (cam == null || _targetObject == null)
                return;

            PlaceModelAtScreenCenter();

            Vector3 flatForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude > 1e-6f)
            {
                Quaternion baseRot = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
                Quaternion targetRot = baseRot * Quaternion.Euler(0f, _modelRotationOffset, 0f);
                _targetObject.transform.rotation = Quaternion.Slerp(_targetObject.transform.rotation, targetRot, Time.deltaTime * 8f);
            }

            EnsureMinimumScale(_targetObject);

            if (!_loggedInsufficientLandmarks)
            {
                _loggedInsufficientLandmarks = true;
                Debug.LogWarning("[PointListAnnotation] Insufficient pose landmarks for full body alignment. Using camera-front fallback.");
            }
        }

        private void PlaceModelAtScreenCenter()
        {
            Camera cam = Camera.main;
            if (cam == null || _targetObject == null)
                return;

            float depth = Mathf.Max(_fallbackDepthMeters, cam.nearClipPlane + 0.5f);
            Vector3 targetAnchor = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
            AlignModelAnchorToWorldPoint(_targetObject, targetAnchor, _modelAnchorHeight01);
            _smoothedDepthMeters = depth;
        }

        private bool HasReliableBodyTracking(IList<NormalizedLandmark> targets)
        {
            if (targets == null || targets.Count < 13)
            {
                return false;
            }

            foreach (int idx in _requiredBodyLandmarkIndices)
            {
                if (idx < 0 || idx >= targets.Count)
                {
                    return false;
                }

                var lm = targets[idx];
                if (lm == null)
                {
                    return false;
                }

                if (!IsFinite(lm.X) || !IsFinite(lm.Y) || !IsFinite(lm.Z))
                {
                    return false;
                }

                // Some pipelines do not populate Visibility/Presence (remain 0).
                // In that case, fall back to coordinate validity only.
                float confidence = Mathf.Max(lm.Visibility, lm.Presence);
                if (confidence > 1e-5f && confidence < _bodyVisibilityThreshold)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryLocalPointToViewport(Vector3 localPoint, out Vector2 viewportPoint)
        {
            UnityEngine.Rect rect = GetScreenRect();
            if (rect.width <= 1e-5f || rect.height <= 1e-5f)
            {
                viewportPoint = new Vector2(0.5f, 0.5f);
                return false;
            }

            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

            if (!IsFinite(x) || !IsFinite(y))
            {
                viewportPoint = new Vector2(0.5f, 0.5f);
                return false;
            }

            viewportPoint = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
            return true;
        }

        private float ComputeDepthFromTorsoHeight(float torsoHeightLocal, Camera cam)
        {
            UnityEngine.Rect rect = GetScreenRect();
            float normalizedTorso = 0f;
            if (rect.height > 1e-4f)
            {
                normalizedTorso = Mathf.Abs(torsoHeightLocal) / rect.height;
            }
            normalizedTorso = Mathf.Clamp(normalizedTorso, 0.01f, 0.95f);

            float nearDepth = Mathf.Max(cam.nearClipPlane + 0.8f, 1.0f);
            float farDepth = Mathf.Max(_fallbackDepthMeters, nearDepth + 0.5f);
            float t = Mathf.InverseLerp(0.10f, 0.45f, normalizedTorso);
            float depth = Mathf.Lerp(farDepth, nearDepth, t);
            if (!IsFinite(depth))
            {
                depth = farDepth;
            }
            return depth;
        }

        private float SmoothDepthMeters(float targetDepth)
        {
            if (!IsFinite(targetDepth) || targetDepth <= 0f)
            {
                targetDepth = Mathf.Max(_fallbackDepthMeters, 1.0f);
            }

            if (!IsFinite(_smoothedDepthMeters) || _smoothedDepthMeters <= 0f)
            {
                _smoothedDepthMeters = targetDepth;
                return _smoothedDepthMeters;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(1f, _depthLerpSpeed) * Time.deltaTime);
            _smoothedDepthMeters = Mathf.Lerp(_smoothedDepthMeters, targetDepth, t);
            return _smoothedDepthMeters;
        }

        private bool TryGetBodySizeAtDepth(
            Camera cam,
            Vector3 leftShoulderLocal,
            Vector3 rightShoulderLocal,
            Vector3 leftHipLocal,
            Vector3 rightHipLocal,
            float depth,
            out float shoulderWidth,
            out float bodyHeight)
        {
            shoulderWidth = 0f;
            bodyHeight = 0f;

            if (!TryLocalPointToViewport(leftShoulderLocal, out Vector2 lsVp) ||
                !TryLocalPointToViewport(rightShoulderLocal, out Vector2 rsVp) ||
                !TryLocalPointToViewport(leftHipLocal, out Vector2 lhVp) ||
                !TryLocalPointToViewport(rightHipLocal, out Vector2 rhVp))
            {
                return false;
            }

            Vector3 leftShoulderWorld = cam.ViewportToWorldPoint(new Vector3(lsVp.x, lsVp.y, depth));
            Vector3 rightShoulderWorld = cam.ViewportToWorldPoint(new Vector3(rsVp.x, rsVp.y, depth));
            Vector3 leftHipWorld = cam.ViewportToWorldPoint(new Vector3(lhVp.x, lhVp.y, depth));
            Vector3 rightHipWorld = cam.ViewportToWorldPoint(new Vector3(rhVp.x, rhVp.y, depth));

            shoulderWidth = Vector3.Distance(leftShoulderWorld, rightShoulderWorld);
            Vector3 topCenter = (leftShoulderWorld + rightShoulderWorld) * 0.5f;
            Vector3 bottomCenter = (leftHipWorld + rightHipWorld) * 0.5f;
            bodyHeight = Vector3.Distance(topCenter, bottomCenter);

            return IsFinite(shoulderWidth) && IsFinite(bodyHeight) && shoulderWidth > 1e-4f && bodyHeight > 1e-4f;
        }

        private void NormalizeModelOnSet(GameObject model)
        {
            if (!_normalizeModelOnSet || model == null)
                return;

            if (!TryGetRendererBounds(model, out Bounds bounds))
                return;

            Vector3 originalCenter = bounds.center;
            Vector3 originalSize = bounds.size;
            bool recentred = false;
            bool rescaled = false;

            bool farFromOrigin =
                Mathf.Abs(bounds.center.x) > _normalizeMaxCenterDistance ||
                Mathf.Abs(bounds.center.z) > _normalizeMaxCenterDistance ||
                bounds.center.magnitude > _normalizeMaxCenterDistance * 1.5f;

            if (farFromOrigin)
            {
                MoveModelBottomCenter(model, Vector3.zero);
                recentred = true;
                TryGetRendererBounds(model, out bounds);
            }

            float height = Mathf.Max(bounds.size.y, 1e-4f);
            if (height < _normalizeMinHeight || height > _normalizeMaxHeight)
            {
                float desiredHeight = Mathf.Max(_targetModelHeight, 0.1f);
                float scaleFactor = Mathf.Clamp(desiredHeight / height, 0.01f, 500f);
                model.transform.localScale *= scaleFactor;
                rescaled = true;

                TryGetRendererBounds(model, out bounds);
                MoveModelBottomCenter(model, Vector3.zero);
                TryGetRendererBounds(model, out bounds);
            }

            if (recentred || rescaled)
            {
                Debug.Log(
                    "[PointListAnnotation] Normalized model on set. " +
                    $"center=({originalCenter.x:F2},{originalCenter.y:F2},{originalCenter.z:F2}) -> " +
                    $"({bounds.center.x:F2},{bounds.center.y:F2},{bounds.center.z:F2}), " +
                    $"size=({originalSize.x:F2},{originalSize.y:F2},{originalSize.z:F2}) -> " +
                    $"({bounds.size.x:F2},{bounds.size.y:F2},{bounds.size.z:F2}), " +
                    $"scale={model.transform.localScale}");
            }
        }

        private static void MoveModelBottomCenter(GameObject model, Vector3 targetBottomCenter)
        {
            if (model == null) return;
            if (!TryGetRendererBounds(model, out Bounds bounds)) return;

            Vector3 currentBottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 delta = targetBottomCenter - currentBottomCenter;
            model.transform.position += delta;
        }

        private static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            bounds = new Bounds();
            if (model == null) return false;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return hasBounds;
        }

        private Vector3 EnsurePointInFrontOfCamera(Vector3 point)
        {
            Camera cam = Camera.main;
            if (cam == null) return point;

            if (!IsFinite(point))
            {
                return cam.transform.position + cam.transform.forward * Mathf.Max(_fallbackDepthMeters, cam.nearClipPlane + 0.3f);
            }

            Vector3 view = cam.WorldToViewportPoint(point);
            bool invalid = !IsFinite(view);
            bool behind = view.z <= cam.nearClipPlane + 0.05f;
            bool extreme = view.z > 1000f;
            bool offscreen = !invalid && (view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f);
            if (!invalid && !behind && !extreme && !offscreen)
            {
                return point;
            }

            float x = (invalid || offscreen) ? 0.5f : Mathf.Clamp01(view.x);
            float y = (invalid || offscreen) ? 0.5f : Mathf.Clamp01(view.y);
            float depth = Mathf.Max(_fallbackDepthMeters, cam.nearClipPlane + 0.3f);
            Vector3 fallback = cam.ViewportToWorldPoint(new Vector3(x, y, depth));

            if (!_loggedVisibilityFallback)
            {
                _loggedVisibilityFallback = true;
                Debug.LogWarning("[PointListAnnotation] Model anchor was behind/offscreen/invalid. Applying camera-front fallback.");
            }

            return fallback;
        }

        private void LogModelRenderDiagnostics(GameObject model)
        {
            if (model == null) return;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            int rendererCount = renderers.Length;
            int enabledRendererCount = 0;
            int materialCount = 0;
            int missingShaderCount = 0;
            bool hasBounds = false;
            Bounds bounds = new Bounds();
            var shaderNames = new HashSet<string>();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.enabled) enabledRendererCount++;

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }

                var sharedMaterials = r.sharedMaterials;
                if (sharedMaterials == null) continue;

                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material mat = sharedMaterials[i];
                    if (mat == null) continue;
                    materialCount++;

                    if (mat.shader == null)
                    {
                        missingShaderCount++;
                    }
                    else
                    {
                        shaderNames.Add(mat.shader.name);
                    }
                }
            }

            var sb = new StringBuilder(256);
            sb.Append("[PointListAnnotation] Model diagnostics: renderers=")
              .Append(rendererCount)
              .Append(", enabled=")
              .Append(enabledRendererCount)
              .Append(", materials=")
              .Append(materialCount)
              .Append(", missingShaderMats=")
              .Append(missingShaderCount);

            if (hasBounds)
            {
                sb.Append(", boundsCenter=(")
                  .Append(bounds.center.x.ToString("F2")).Append(", ")
                  .Append(bounds.center.y.ToString("F2")).Append(", ")
                  .Append(bounds.center.z.ToString("F2")).Append(")")
                  .Append(", boundsSize=(")
                  .Append(bounds.size.x.ToString("F2")).Append(", ")
                  .Append(bounds.size.y.ToString("F2")).Append(", ")
                  .Append(bounds.size.z.ToString("F2")).Append(")");
            }

            int loggedShaderCount = 0;
            if (shaderNames.Count > 0)
            {
                sb.Append(", shaders=");
                foreach (string shaderName in shaderNames)
                {
                    if (loggedShaderCount > 0)
                    {
                        sb.Append("|");
                    }
                    sb.Append(shaderName);
                    loggedShaderCount++;
                    if (loggedShaderCount >= 6) break;
                }
            }

            Debug.Log(sb.ToString());
        }

        private void EnsureMinimumScale(GameObject model)
        {
            if (model == null) return;

            float minScale = Mathf.Max(0.01f, _minimumModelScale);
            Vector3 s = model.transform.localScale;
            float uniform = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            if (uniform >= minScale) return;

            model.transform.localScale = Vector3.one * minScale;
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        private void WearSegment(Vector3 highJointMP, Vector3 lowJointMP, List<Transform> boneTransforms)
        {
            if (boneTransforms.Count == 0) return;

            Vector3 high = MPToUnity(highJointMP);
            Vector3 low = MPToUnity(lowJointMP);
            Vector3 worldDir = (low - high).normalized;
            if (worldDir.sqrMagnitude < 1e-6f) return;

            foreach (var bone in boneTransforms)
            {
                if (bone == null || bone.parent == null) continue;
                
                Vector3 localDir = bone.parent.InverseTransformDirection(worldDir);
                Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, localDir);
                bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRot, Time.deltaTime * 15f);
            }
        }

        private void WearLeftUpperArm() => WearSegment(children[11].transform.position, children[13].transform.position, leftupperArmObjs);
        private void WearLeftlowerArm() => WearSegment(children[13].transform.position, children[15].transform.position, leftlowerArmObjs);
        private void WearRightUpperArm() => WearSegment(children[12].transform.position, children[14].transform.position, rightupperArmObjs);
        private void WearRightlowerArm() => WearSegment(children[14].transform.position, children[16].transform.position, rightlowerArmObjs);

        protected override PointAnnotation InstantiateChild(bool isActive = true)
        {
            var annotation = base.InstantiateChild(isActive);
            annotation.SetColor(_color);
            annotation.SetRadius(_radius);
            return annotation;
        }

        private void ApplyColor(UnityEngine.Color color)
        {
            foreach (var point in children) point?.SetColor(color);
        }

        private void ApplyRadius(float radius)
        {
            foreach (var point in children) point?.SetRadius(radius);
        }
    }
}
