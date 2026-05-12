using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace HW2.HandMotionExperiment
{
    public enum HandCondition
    {
        DominantHand,
        NonDominantHand
    }

    public enum DominantHandSetting
    {
        Right,
        Left
    }

    public enum TargetMotionMode
    {
        Static,
        Slow,
        Fast
    }

    [Serializable]
    public sealed class TrialDefinition
    {
        public int TrialNumber;
        public int Repetition;
        public HandCondition Hand;
        public TargetMotionMode MotionMode;
        public float TargetSpeedMetersPerSecond;
        public Vector2 DirectionDegrees;
    }

    public sealed class TrialResult
    {
        public TrialDefinition Definition;
        public float MovementTimeSeconds;
        public bool Hit;
        public Vector3 StartPoint;
        public Vector3 TargetPosition;
        public string FailureReason;
    }

    [Serializable]
    public sealed class ExperimentConfig
    {
        public string GroupName = "OregonShooters";
        public int ParticipantIndex = 0;
        public DominantHandSetting DominantHand = DominantHandSetting.Right;
        public int Repetitions = 5;
        public float TrialTimeoutSeconds = 4f;
        public float TargetDiameterMeters = 0.14f;
        public float TargetDistanceMeters = 1.0f;
        public float MotionRangeMeters = 0.45f;
        public float SlowSpeedMetersPerSecond = 0.25f;
        public float FastSpeedMetersPerSecond = 0.75f;

        public Vector2[] DirectionsDegrees =
        {
            new Vector2(-20f, 8f),
            new Vector2(20f, 8f),
            new Vector2(-12f, -6f),
            new Vector2(12f, -6f)
        };

        public List<TrialDefinition> BuildTrials()
        {
            var trials = new List<TrialDefinition>();
            int trialNumber = 1;

            var hands = BuildHandOrder();
            var conditions = new List<TrialCondition>();

            foreach (var hand in hands)
            {
                conditions.Add(new TrialCondition(hand, TargetMotionMode.Static, 0f));
                conditions.Add(new TrialCondition(hand, TargetMotionMode.Slow, SlowSpeedMetersPerSecond));
                conditions.Add(new TrialCondition(hand, TargetMotionMode.Fast, FastSpeedMetersPerSecond));
            }

            for (int rep = 0; rep < Repetitions; rep++)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    TrialCondition condition = conditions[i];
                    int directionIndex = (i + rep) % DirectionsDegrees.Length;

                    trials.Add(new TrialDefinition
                    {
                        TrialNumber = trialNumber++,
                        Repetition = rep + 1,
                        Hand = condition.Hand,
                        MotionMode = condition.MotionMode,
                        TargetSpeedMetersPerSecond = condition.Speed,
                        DirectionDegrees = DirectionsDegrees[directionIndex]
                    });
                }
            }

            return trials;
        }

        private List<HandCondition> BuildHandOrder()
        {
            var hands = new List<HandCondition>
            {
                HandCondition.DominantHand,
                HandCondition.NonDominantHand
            };

            if (Mathf.Abs(ParticipantIndex) % 2 == 1)
            {
                hands.Reverse();
            }

            return hands;
        }

        private struct TrialCondition
        {
            public HandCondition Hand;
            public TargetMotionMode MotionMode;
            public float Speed;

            public TrialCondition(HandCondition hand, TargetMotionMode motionMode, float speed)
            {
                Hand = hand;
                MotionMode = motionMode;
                Speed = speed;
            }
        }
    }

    public static class HandMotionExperimentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateManager()
        {
            if (UnityEngine.Object.FindFirstObjectByType<HandMotionExperimentManager>() != null)
            {
                return;
            }

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            var root = new GameObject("HandMotionExperimentRuntime");
            root.AddComponent<HandMotionExperimentManager>();
        }
    }

    public sealed class HandMotionExperimentManager : MonoBehaviour
    {
        private ExperimentConfig _config;
        private readonly List<TrialResult> _results = new List<TrialResult>();

        private Camera _mainCamera;
        private Transform _panelTransform;
        private TMP_Text _trialText;
        private TMP_Text _methodText;
        private TMP_Text _resultText;
        private TMP_Text _pathText;
        private GameObject _targetSphere;
        private Renderer _targetRenderer;
        private LineRenderer _pointerLine;

        private List<TrialDefinition> _trials;
        private TrialDefinition _currentTrial;
        private int _trialIndex = -1;
        private float _trialStartTime;
        private Vector3 _trialStartPoint;
        private Vector3 _fixedStartPoint;
        private Vector3 _targetBasePosition;
        private Vector3 _motionAxis;
        private bool _controllerWasPressed;
        private bool _trialEnding;
        private bool _waitingForHandStart;
        private string _rawCsvPath;
        private string _summaryCsvPath;

        private static readonly Color DefaultTargetColor = new Color(0.2f, 0.45f, 0.9f, 1f);
        private static readonly Color HoverTargetColor = new Color(0.15f, 0.85f, 0.35f, 1f);
        private static readonly Color HitColor = new Color(0.25f, 0.9f, 0.35f, 1f);
        private static readonly Color MissColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private IEnumerator Start()
        {
            Debug.Log("HAND MOTION START() RUNNING");

            yield return null;
            yield return null;

            _fixedStartPoint = new Vector3(0f, 1.2f, 0.6f);

            _mainCamera = FindXRCamera();
            if (_mainCamera == null)
            {
                Debug.LogError("No XR camera found. Keep the XR Origin (VR) camera active and tagged MainCamera.");
                yield break;
            }

            Debug.Log("Hand motion experiment using camera: " + GetHierarchyPath(_mainCamera.transform));

            BuildEnvironment();
            BuildHud();
            BuildPointer();
            BuildTargetSphere();

            _config = new ExperimentConfig();
            _trials = _config.BuildTrials();

            Debug.Log("Hand motion trials count: " + (_trials == null ? "NULL" : _trials.Count.ToString()));

            _trialText.text = string.Empty;
            _methodText.text = string.Empty;
            _pathText.text = string.Empty;

            if (_trials != null && _trials.Count > 0)
            {
                ShowHandStartScreen(_trials[0].Hand);
            }
            else
            {
                _resultText.text = "No trials configured";
                _resultText.color = MissColor;
            }
        }

        private void Update()
        {
            if (_mainCamera == null)
            {
                return;
            }

            UpdateHudPose();

            if (_waitingForHandStart)
            {
                if (IsStartPressed())
                {
                    StartNextTrial();
                }

                return;
            }

            if (_currentTrial == null)
            {
                return;
            }

            UpdateTargetMotion();
            HandleCurrentTrial();
        }

        private Camera FindXRCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.stereoTargetEye != StereoTargetEyeMask.None)
                {
                    return camera;
                }
            }

            foreach (Camera camera in cameras)
            {
                if (camera != null && camera.enabled && camera.CompareTag("MainCamera"))
                {
                    return camera;
                }
            }

            return Camera.main;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "null";
            }

            var names = new List<string>();
            Transform current = transform;

            while (current != null)
            {
                names.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private void BuildEnvironment()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "RuntimeFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            floor.GetComponent<Renderer>().material.color = new Color(0.83f, 0.83f, 0.86f, 1f);

            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "RuntimeTable";
            table.transform.position = new Vector3(0f, 0.82f, 0.85f);
            table.transform.localScale = new Vector3(1.15f, 0.08f, 0.6f);
            table.GetComponent<Renderer>().material.color = new Color(0.49f, 0.36f, 0.25f, 1f);

            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = "RuntimeWall";
            backWall.transform.position = new Vector3(0f, 1.3f, 2.2f);
            backWall.transform.localScale = new Vector3(3.5f, 2.8f, 0.08f);
            backWall.GetComponent<Renderer>().material.color = new Color(0.94f, 0.95f, 0.98f, 1f);
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("ExperimentHUD");
            canvasObject.layer = 0;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _mainCamera;
            canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500f, 260f);
            _panelTransform = canvas.transform;

            var panel = new GameObject("Panel");
            panel.layer = 0;
            panel.transform.SetParent(canvas.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.09f, 0.14f, 0.8f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            _trialText = CreateText(panel.transform, new Vector2(0f, 70f), 24, TextAlignmentOptions.TopLeft);
            _methodText = CreateText(panel.transform, new Vector2(0f, 20f), 22, TextAlignmentOptions.TopLeft);
            _resultText = CreateText(panel.transform, new Vector2(0f, -35f), 24, TextAlignmentOptions.MidlineLeft);
            _pathText = CreateText(panel.transform, new Vector2(0f, -95f), 18, TextAlignmentOptions.TopLeft);
            _pathText.enableWordWrapping = true;

            UpdateHudPose();
            _resultText.text = "Waiting to start";
            _resultText.color = Color.white;
        }

        private void BuildPointer()
        {
            var pointerObject = new GameObject("PointerRay");
            _pointerLine = pointerObject.AddComponent<LineRenderer>();
            _pointerLine.positionCount = 2;
            _pointerLine.startWidth = 0.01f;
            _pointerLine.endWidth = 0.004f;
            _pointerLine.material = new Material(Shader.Find("Sprites/Default"));
            _pointerLine.startColor = Color.white;
            _pointerLine.endColor = new Color(1f, 1f, 1f, 0.2f);
            _pointerLine.enabled = false;
        }

        private void BuildTargetSphere()
        {
            _targetSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _targetSphere.name = "TargetSphere";
            _targetRenderer = _targetSphere.GetComponent<Renderer>();
            _targetRenderer.material.color = DefaultTargetColor;
            _targetSphere.SetActive(false);
        }

        private void ShowHandStartScreen(HandCondition hand)
        {
            _waitingForHandStart = true;
            _currentTrial = null;
            _pointerLine.enabled = false;
            _targetSphere.SetActive(false);

            _trialText.text = string.Empty;
            _methodText.text = string.Empty;
            _resultText.text = "Press Start when ready to begin the " + FormatHand(hand).ToLowerInvariant() + " block.";
            _resultText.color = Color.white;
            _pathText.text = "Use only the assigned hand for this block.";
        }

        private void StartNextTrial()
        {
            _waitingForHandStart = false;
            _trialEnding = false;
            _pointerLine.enabled = true;

            if (_targetSphere == null)
            {
                Debug.LogWarning("Target was null - rebuilding...");
                BuildTargetSphere();
            }

            if (_trials == null || _trials.Count == 0)
            {
                Debug.LogError("Trials not initialized!");
                return;
            }

            _trialIndex++;
            _controllerWasPressed = false;

            if (_trialIndex >= _trials.Count)
            {
                FinishExperiment();
                return;
            }

            _currentTrial = _trials[_trialIndex];

            if (_trialIndex > 0 && _currentTrial.Hand != _trials[_trialIndex - 1].Hand)
            {
                ShowHandStartScreen(_currentTrial.Hand);
                return;
            }

            _trialStartTime = Time.time;
            _trialStartPoint = GetStartPoint();
            _targetBasePosition = ComputeTargetPosition(_currentTrial, _trialStartPoint);
            _motionAxis = Vector3.ProjectOnPlane(_mainCamera.transform.right, Vector3.up).normalized;
            if (_motionAxis.sqrMagnitude < 0.001f)
            {
                _motionAxis = Vector3.right;
            }

            _targetSphere.transform.position = _targetBasePosition;
            _targetSphere.transform.localScale = Vector3.one * _config.TargetDiameterMeters;
            _targetRenderer.material.color = DefaultTargetColor;
            _targetSphere.SetActive(true);

            _trialText.text = $"Trial {_currentTrial.TrialNumber} / {_trials.Count}";
            _methodText.text =
                $"Hand: {FormatHand(_currentTrial.Hand)}\n" +
                $"Motion: {FormatMotion(_currentTrial.MotionMode)}  Speed: {_currentTrial.TargetSpeedMetersPerSecond:0.00} m/s";
            _resultText.text = string.Empty;
            _resultText.color = Color.white;
            _pathText.text = string.Empty;
        }

        private void UpdateTargetMotion()
        {
            if (_currentTrial == null || _targetSphere == null)
            {
                return;
            }

            if (_currentTrial.MotionMode == TargetMotionMode.Static)
            {
                _targetSphere.transform.position = _targetBasePosition;
                return;
            }

            float elapsed = Time.time - _trialStartTime;
            float range = Mathf.Max(0.01f, _config.MotionRangeMeters);
            float speed = Mathf.Max(0.01f, _currentTrial.TargetSpeedMetersPerSecond);
            float period = (range * 2f) / speed;
            float offset = Mathf.Sin((elapsed / period) * Mathf.PI * 2f) * range * 0.5f;

            _targetSphere.transform.position = _targetBasePosition + (_motionAxis * offset);
        }

        private void HandleCurrentTrial()
        {
            XRNode node = GetControllerNode(_currentTrial.Hand);
            var pointerRay = GetPointerRay(node);
            bool hovering = Physics.Raycast(pointerRay, out RaycastHit hitInfo, 5f) &&
                hitInfo.collider != null &&
                hitInfo.collider.gameObject == _targetSphere;

            _targetRenderer.material.color = hovering ? HoverTargetColor : DefaultTargetColor;
            Vector3 hitPoint = hovering
                ? hitInfo.point
                : pointerRay.origin + pointerRay.direction * 3f;

            UpdatePointer(pointerRay, hovering, hitPoint);

            float elapsed = Time.time - _trialStartTime;

            if (elapsed >= _config.TrialTimeoutSeconds)
            {
                CompleteTrial(false, "Timeout");
                return;
            }

            bool controllerPressed = IsControllerSelectPressed(node);

            if (!_trialEnding && controllerPressed && !_controllerWasPressed)
            {
                CompleteTrial(hovering, hovering ? "Hit" : "Miss");
            }

            _controllerWasPressed = controllerPressed;
        }

        private void CompleteTrial(bool hit, string reason)
        {
            if (_trialEnding) return;
            _trialEnding = true;
            var definition = _currentTrial;

            float movementTime = Time.time - _trialStartTime;

            _currentTrial = null;

            var result = new TrialResult
            {
                Definition = definition,
                MovementTimeSeconds = movementTime,
                Hit = hit,
                StartPoint = _trialStartPoint,
                TargetPosition = _targetSphere != null ? _targetSphere.transform.position : Vector3.zero,
                FailureReason = hit ? string.Empty : reason
            };

            _results.Add(result);
            _targetSphere.SetActive(false);
            _resultText.text = hit ? $"Hit in {movementTime:0.000}s" : $"{reason} in {movementTime:0.000}s";
            _resultText.color = hit ? HitColor : MissColor;

            CancelInvoke(nameof(StartNextTrial));
            Invoke(nameof(StartNextTrial), 1.0f);
        }

        private void FinishExperiment()
        {
            _currentTrial = null;
            _pointerLine.enabled = false;
            WriteCsvFiles();
            _trialText.text = "Experiment complete";
            _methodText.text = $"Completed {_results.Count} trials";
            _resultText.text = "CSV export finished";
            _resultText.color = HitColor;
            _pathText.text = $"Raw: {_rawCsvPath}\nSummary: {_summaryCsvPath}";
        }

        private void WriteCsvFiles()
        {
            string baseFileName = $"{_config.GroupName}_HandMotion_Outputfile";
            _rawCsvPath = Path.Combine(Application.persistentDataPath, baseFileName + ".csv");
            _summaryCsvPath = Path.Combine(Application.persistentDataPath, baseFileName + "_Summary.csv");

            var rawBuilder = new StringBuilder();
            rawBuilder.AppendLine("participant,trial,hand,repetition,motion_mode,target_speed_m_s,start_x,start_y,start_z,target_x,target_y,target_z,movement_time_s,hit,failure_reason");

            foreach (var result in _results)
            {
                TrialDefinition definition = result.Definition;
                rawBuilder.AppendLine(string.Join(",",
                    _config.ParticipantIndex,
                    definition.TrialNumber,
                    FormatHand(definition.Hand),
                    definition.Repetition,
                    FormatMotion(definition.MotionMode),
                    FormatFloat(definition.TargetSpeedMetersPerSecond),
                    FormatFloat(result.StartPoint.x),
                    FormatFloat(result.StartPoint.y),
                    FormatFloat(result.StartPoint.z),
                    FormatFloat(result.TargetPosition.x),
                    FormatFloat(result.TargetPosition.y),
                    FormatFloat(result.TargetPosition.z),
                    FormatFloat(result.MovementTimeSeconds),
                    result.Hit ? "1" : "0",
                    EscapeCsv(result.FailureReason)));
            }

            File.WriteAllText(_rawCsvPath, rawBuilder.ToString());

            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine("hand,motion_mode,target_speed_m_s,mean_movement_time_s,error_rate_percent,total_trials,total_hits");

            var byCondition = _results.GroupBy(r => new
            {
                r.Definition.Hand,
                r.Definition.MotionMode,
                r.Definition.TargetSpeedMetersPerSecond
            });

            foreach (var conditionGroup in byCondition)
            {
                var all = conditionGroup.ToList();
                var hits = all.Where(r => r.Hit).ToList();

                float meanMt = hits.Count > 0
                    ? hits.Average(r => r.MovementTimeSeconds)
                    : float.NaN;

                float total = all.Count;
                float misses = all.Count(r => !r.Hit);
                float errorRate = total > 0 ? (100f * misses / total) : 0f;

                summaryBuilder.AppendLine(string.Join(",",
                    FormatHand(conditionGroup.Key.Hand),
                    FormatMotion(conditionGroup.Key.MotionMode),
                    FormatFloat(conditionGroup.Key.TargetSpeedMetersPerSecond),
                    FormatFloat(meanMt),
                    FormatFloat(errorRate),
                    all.Count,
                    hits.Count));
            }

            File.WriteAllText(_summaryCsvPath, summaryBuilder.ToString());
        }

        private Vector3 ComputeTargetPosition(TrialDefinition trial, Vector3 anchor)
        {
            Vector3 yawed = Quaternion.AngleAxis(trial.DirectionDegrees.x, Vector3.up) * _mainCamera.transform.forward;
            Vector3 pitched = Quaternion.AngleAxis(-trial.DirectionDegrees.y, _mainCamera.transform.right) * yawed;

            return anchor + pitched.normalized * _config.TargetDistanceMeters;
        }

        private Vector3 GetStartPoint()
        {
            return _fixedStartPoint;
        }

        private XRNode GetControllerNode(HandCondition hand)
        {
            bool dominantIsRight = _config.DominantHand == DominantHandSetting.Right;
            bool useRightHand = dominantIsRight;

            if (hand == HandCondition.NonDominantHand)
            {
                useRightHand = !dominantIsRight;
            }

            return useRightHand ? XRNode.RightHand : XRNode.LeftHand;
        }

        private Ray GetPointerRay(XRNode node)
        {
            if (TryGetXRRay(node, out Ray ray))
            {
                return ray;
            }

            return new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
        }

        private bool TryGetXRRay(XRNode node, out Ray ray)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position);
            bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);

            if (hasPosition && hasRotation)
            {
                ray = new Ray(position, rotation * Vector3.forward);
                return true;
            }

            ray = default;
            return false;
        }

        private bool IsStartPressed()
        {
            return Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetMouseButtonDown(0) ||
                IsControllerSelectPressed(XRNode.RightHand) ||
                IsControllerSelectPressed(XRNode.LeftHand);
        }

        private bool IsControllerSelectPressed(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed))
            {
                return triggerPressed;
            }

            if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
            {
                return triggerValue > 0.7f;
            }

            return Input.GetMouseButtonDown(0) || Input.GetKey(KeyCode.Space);
        }

        private void UpdateHudPose()
        {
            Vector3 forwardFlat = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
            if (forwardFlat.sqrMagnitude < 0.001f)
            {
                forwardFlat = Vector3.forward;
            }

            _panelTransform.position = _mainCamera.transform.position + (forwardFlat * 0.9f) + (Vector3.up * 0.02f);
            _panelTransform.LookAt(_mainCamera.transform);
            _panelTransform.Rotate(0f, 180f, 0f);
            _panelTransform.localScale = Vector3.one * 0.0013f;
        }

        private void UpdatePointer(Ray ray, bool hovering, Vector3 hitPoint)
        {
            _pointerLine.enabled = _currentTrial != null;
            _pointerLine.startColor = hovering ? HoverTargetColor : Color.white;
            _pointerLine.endColor = hovering ? HoverTargetColor : new Color(1f, 1f, 1f, 0.2f);
            _pointerLine.SetPosition(0, ray.origin);
            _pointerLine.SetPosition(1, hovering ? hitPoint : ray.origin + (ray.direction.normalized * 3f));
        }

        private static TMP_Text CreateText(Transform parent, Vector2 anchoredPosition, int fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject("Text");
            textObject.layer = 0;
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;

            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(460f, 50f);
            rect.anchoredPosition = anchoredPosition;
            return text;
        }

        private static string FormatHand(HandCondition hand)
        {
            switch (hand)
            {
                case HandCondition.DominantHand:
                    return "Dominant Hand";
                case HandCondition.NonDominantHand:
                    return "Non-Dominant Hand";
                default:
                    return hand.ToString();
            }
        }

        private static string FormatMotion(TargetMotionMode motionMode)
        {
            switch (motionMode)
            {
                case TargetMotionMode.Static:
                    return "Static";
                case TargetMotionMode.Slow:
                    return "Slow Moving";
                case TargetMotionMode.Fast:
                    return "Fast Moving";
                default:
                    return motionMode.ToString();
            }
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}