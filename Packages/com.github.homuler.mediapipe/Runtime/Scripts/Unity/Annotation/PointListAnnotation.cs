using System.Collections.Generic;
using UnityEngine;
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
        [SerializeField] private bool _forceVisibleInFrontOfCamera = true;
        [SerializeField] private float _fallbackDepthMeters = 2.5f;
        [SerializeField] private float _minimumModelScale = 0.6f;

        [Header("Visibility Stability")]
        [SerializeField] private int _visibilityBufferFrames = 10; // Number of frames to wait before hiding model
        private int _visibilityCounter = 0;
        private bool _loggedVisibilityFallback = false;

        private Vector3 _lastForward = Vector3.forward;
        private bool _isScaled = false;
        private Vector3 _modelBaseOffset;

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

        public void SetModel(GameObject newModel)
        {
            if (newModel == null) return;

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

            BindBones(_targetObject);
            CenterModel(_targetObject);

            _targetObject.transform.rotation = Quaternion.Euler(0, _modelRotationOffset, 0);
            _modelBaseOffset = _targetObject.transform.position;
            
            _visibilityCounter = _visibilityBufferFrames; // Initialize buffer
        }

        public void Draw(IList<NormalizedLandmark> targets, bool visualizeZ = true)
        {
            bool isTracking = (targets != null && targets.Count > 0);
            
            if (isTracking)
            {
                _visibilityCounter = _visibilityBufferFrames;
                SetActive(true);
                
                CallActionForAll(targets, (annotation, target) =>
                {
                    annotation?.Draw(target, visualizeZ);
                });
            }
            else
            {
                if (_visibilityCounter > 0)
                {
                    _visibilityCounter--;
                }
                else
                {
                    SetActive(false);
                }
            }
        }

        private void LateUpdate()
        {
            if (isActive && _targetObject != null)
            {
                UpdateModelPose();
            }
        }

        private void UpdateModelPose()
        {
            WearRightUpperArm();
            WearLeftUpperArm();
            WearRightlowerArm();
            WearLeftlowerArm();

            if (_targetObject == null || children.Count < 25)
                return;

            UpdateModelTransformFromChildren();
        }

        private void AlignModelToBottomCenter(GameObject model, Vector3 bottomCenter)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (var rend in renderers)
                bounds.Encapsulate(rend.bounds);

            float modelBottomY = bounds.min.y;
            float offsetY = bottomCenter.y - modelBottomY;

            model.transform.position = new Vector3(bottomCenter.x, model.transform.position.y + offsetY, bottomCenter.z);
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

        private void FitModelScale(GameObject model, float targetWidth, float targetHeight)
        {
            Vector3 orig = GetModelOriginalSize(model);

            float scaleX = targetWidth / orig.x;
            float scaleY = targetHeight / orig.y;
            float scaleZ = (orig.z > 0.001f) ? scaleY * (orig.z / orig.y) : 1f;

            float scale = (scaleX + scaleY + scaleZ) / 3f;

            model.transform.localScale = Vector3.one * scale * _scaleMultiplier;
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

        public void Draw(IList<Vector3> targets)
        {
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
            else if (_visibilityCounter <= 0)
            {
                SetActive(false);
            }
        }

        public void Draw(IList<Landmark> targets, Vector3 scale, bool visualizeZ = true)
        {
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
            else if (_visibilityCounter <= 0)
            {
                SetActive(false);
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
            if (targets != null && targets.Count > 0)
            {
                _visibilityCounter = _visibilityBufferFrames;
                SetActive(true);
                CallActionForAll(targets, (annotation, target) =>
                {
                    annotation?.Draw(target, threshold);
                });
            }
            else if (_visibilityCounter <= 0)
            {
                SetActive(false);
            }
        }

        public void Draw(IList<Landmark> worldLandmarks)
        {
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
            else if (_visibilityCounter <= 0)
            {
                SetActive(false);
            }
        }

        private void UpdateModelTransformFromChildren()
        {
            if (_targetObject == null || children.Count < 25)
                return;

            Vector3 rightShoulder = children[12].transform.position;
            Vector3 leftShoulder = children[11].transform.position;
            Vector3 rightHip = children[24].transform.position;
            Vector3 leftHip = children[23].transform.position;

            Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
            Vector3 hipCenter = (leftHip + rightHip) * 0.5f;
            Vector3 bottomCenter = (children[23].transform.position + children[24].transform.position) * 0.5f;

            float yaw = 0f;
            if (_useDynamicRotation)
            {
                Vector3 bodyUp = (shoulderCenter - hipCenter).normalized;
                Vector3 bodyRight = (rightShoulder - leftShoulder).normalized;
                Vector3 bodyForward = Vector3.Cross(bodyRight, bodyUp).normalized;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camUp = cam.transform.up;
                    Vector3 camForward = cam.transform.forward;
                    Vector3 projectedForward = Vector3.ProjectOnPlane(bodyForward, camUp).normalized;
                    yaw = Vector3.SignedAngle(camForward, projectedForward, camUp);
                }
            }

            Quaternion targetRot = Quaternion.Euler(0f, yaw + _modelRotationOffset, 0f);
            _targetObject.transform.rotation = Quaternion.Slerp(_targetObject.transform.rotation, targetRot, Time.deltaTime * 10f);

            if (!_isScaled)
            {
                if (TryGetBodySize(out float shoulderWidth, out float bodyHeight))
                {
                    FitModelScale(_targetObject, shoulderWidth, bodyHeight);
                    _isScaled = true;
                    _modelBaseOffset = hipCenter;
                }
            }

            if (_forceVisibleInFrontOfCamera)
            {
                hipCenter = EnsurePointInFrontOfCamera(hipCenter);
                bottomCenter = EnsurePointInFrontOfCamera(bottomCenter);
            }

            _targetObject.transform.position = hipCenter;
            AlignModelToBottomCenter(_targetObject, bottomCenter);
            EnsureMinimumScale(_targetObject);
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
            if (!invalid && !behind && !extreme)
            {
                return point;
            }

            float x = invalid ? 0.5f : Mathf.Clamp01(view.x);
            float y = invalid ? 0.5f : Mathf.Clamp01(view.y);
            float depth = Mathf.Max(_fallbackDepthMeters, cam.nearClipPlane + 0.3f);
            Vector3 fallback = cam.ViewportToWorldPoint(new Vector3(x, y, depth));

            if (!_loggedVisibilityFallback)
            {
                _loggedVisibilityFallback = true;
                Debug.LogWarning("[PointListAnnotation] Model anchor was behind/invalid. Applying camera-front fallback.");
            }

            return fallback;
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
