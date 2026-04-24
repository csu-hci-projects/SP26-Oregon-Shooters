using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace HW2.FittsLaw
{
    public sealed class FittsExperimentManager : MonoBehaviour
    {
        private ExperimentConfig _config;
        private readonly List<TrialResult> _results = new List<TrialResult>();

        private bool _experimentStarted = false;

        private Camera _mainCamera;
        private Transform _panelTransform;
        private Text _trialText;
        private Text _methodText;
        private Text _resultText;
        private Text _pathText;
        private GameObject _targetSphere;
        private Renderer _targetRenderer;
        private LineRenderer _pointerLine;

        private List<TrialDefinition> _trials;
        private TrialDefinition _currentTrial;
        private int _trialIndex = -1;
        private float _trialStartTime;
        private Vector3 _trialStartPoint;
        private float _gazeHoverStartTime = -1f;
        private bool _controllerWasPressed;
        private string _rawCsvPath;
        private string _summaryCsvPath;
        private Vector3 _fixedStartPoint;
        private bool _trialEnding;

        private static readonly Color DefaultTargetColor = new Color(0.2f, 0.45f, 0.9f, 1f);
        private static readonly Color HoverTargetColor = new Color(0.15f, 0.85f, 0.35f, 1f);
        private static readonly Color HitColor = new Color(0.25f, 0.9f, 0.35f, 1f);
        private static readonly Color MissColor = new Color(0.95f, 0.25f, 0.25f, 1f);

        private void Start()
        {
            Debug.Log("START() RUNNING");

            _fixedStartPoint = new Vector3(0f, 1.2f, 0.6f);

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _mainCamera = cameraObject.AddComponent<Camera>();
                _mainCamera.transform.position = new Vector3(0f, 1.6f, -0.4f);
            }

            BuildEnvironment();
            BuildHud();
            BuildPointer();
            BuildTargetSphere();

            _config = new ExperimentConfig();
            _trials = _config.BuildTrials();

            Debug.Log("Trials object: " + (_trials == null ? "NULL" : "NOT NULL"));
            Debug.Log("Trials count: " + (_trials == null ? "NULL" : _trials.Count.ToString()));

            _resultText.text = "Press SPACE to start experiment";
            _resultText.color = Color.white;

            _trialText.text = "";
            _methodText.text = "";
            _pathText.text = "";
        }

        private void Update()
        {
            if (_mainCamera == null)
                return;

            if (!_experimentStarted)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _experimentStarted = true;
                    StartNextTrial();
                }
                return;
            }

            if (_currentTrial == null)
                return;

            UpdateHudPose();
            HandleCurrentTrial();
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
            canvasObject.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _mainCamera;
            canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 20f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500f, 260f);
            _panelTransform = canvas.transform;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvas.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.09f, 0.14f, 0.8f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            _trialText = CreateText(panel.transform, new Vector2(0f, 70f), 24, TextAnchor.UpperLeft);
            _methodText = CreateText(panel.transform, new Vector2(0f, 20f), 22, TextAnchor.UpperLeft);
            _resultText = CreateText(panel.transform, new Vector2(0f, -35f), 24, TextAnchor.MiddleLeft);
            _pathText = CreateText(panel.transform, new Vector2(0f, -95f), 18, TextAnchor.UpperLeft);
            _pathText.horizontalOverflow = HorizontalWrapMode.Wrap;

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
        }

        private void BuildTargetSphere()
        {
            _targetSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _targetSphere.name = "TargetSphere";
            _targetRenderer = _targetSphere.GetComponent<Renderer>();
            _targetRenderer.material.color = DefaultTargetColor;
            _targetSphere.SetActive(false);
        }

        private void StartNextTrial()
        {
            _trialEnding = false;
            _pointerLine.enabled = true;

            if (_targetSphere == null)
            {
                Debug.LogWarning("Target was null � rebuilding...");
                BuildTargetSphere();
            }

            if (_trials == null || _trials.Count == 0)
            {
                Debug.LogError("Trials not initialized!");
                return;
            }

            _trialIndex++;
            _gazeHoverStartTime = -1f;
            _controllerWasPressed = false;

            if (_trialIndex >= _trials.Count)
            {
                FinishExperiment();
                return;
            }

            _currentTrial = _trials[_trialIndex];
            _trialStartTime = Time.time;
            _trialStartPoint = GetStartPoint();
            _targetSphere.transform.position = ComputeTargetPosition(_currentTrial, _trialStartPoint);
            float diameter = _currentTrial.DiameterMeters;
            _targetSphere.transform.localScale = new Vector3(diameter, diameter, diameter);
            _targetRenderer.material.color = DefaultTargetColor;
            _targetSphere.SetActive(true);

            _trialText.text = $"Trial {_currentTrial.TrialNumber} / {_trials.Count}";
            _methodText.text =
                $"Method: {FormatMethod(_currentTrial.Method)}\n" +
                $"Distance: {_currentTrial.AmplitudeMeters:0.00}m  Size: {_currentTrial.DiameterMeters:0.00}m";
            _resultText.text = "Reach and select the blue sphere";
            _resultText.color = Color.white;
            _pathText.text = string.Empty;
        }

        private void HandleCurrentTrial()
        {
            var pointerRay = GetPointerRay(_currentTrial.Method);
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

            if (_currentTrial.Method == InteractionMethod.HeadGazeDwell)
            {
                if (hovering)
                {
                    if (_gazeHoverStartTime < 0f)
                    {
                        _gazeHoverStartTime = Time.time;
                    }

                    float dwellRemaining = Mathf.Max(0f, _config.GazeDwellSeconds - (Time.time - _gazeHoverStartTime));
                    _resultText.text = $"Hovering... dwell {dwellRemaining:0.00}s";

                    if (Time.time - _gazeHoverStartTime >= _config.GazeDwellSeconds)
                    {
                        CompleteTrial(true, "Hit");
                    }
                }
                else
                {
                    _gazeHoverStartTime = -1f;
                }

                return;
            }

            bool controllerPressed = false;

            switch (_currentTrial.Method)
            {
                case InteractionMethod.ControllerTriggerRay:
                    controllerPressed = IsControllerSelectPressed();
                    break;

                case InteractionMethod.ControllerGripSelect:
                    controllerPressed = IsGripPressed();
                    break;

                default:
                    controllerPressed = IsControllerSelectPressed();
                    break;
            }
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
            string baseFileName = $"{_config.GroupName}_Outputfile";
            _rawCsvPath = Path.Combine(Application.persistentDataPath, baseFileName + ".csv");
            _summaryCsvPath = Path.Combine(Application.persistentDataPath, baseFileName + "_Summary.csv");

            var rawBuilder = new StringBuilder();
            rawBuilder.AppendLine("participant,trial,method,repetition,amplitude_m,diameter_m,direction_yaw_deg,direction_pitch_deg,start_x,start_y,start_z,target_x,target_y,target_z,movement_time_s,hit,index_of_difficulty,throughput_bits_s,failure_reason");

            foreach (var result in _results)
            {
                TrialDefinition definition = result.Definition;
                rawBuilder.AppendLine(string.Join(",",
                    _config.ParticipantIndex,
                    definition.TrialNumber,
                    FormatMethod(definition.Method),
                    definition.Repetition,
                    FormatFloat(definition.AmplitudeMeters),
                    FormatFloat(definition.DiameterMeters),
                    FormatFloat(definition.DirectionDegrees.x),
                    FormatFloat(definition.DirectionDegrees.y),
                    FormatFloat(result.StartPoint.x),
                    FormatFloat(result.StartPoint.y),
                    FormatFloat(result.StartPoint.z),
                    FormatFloat(result.TargetPosition.x),
                    FormatFloat(result.TargetPosition.y),
                    FormatFloat(result.TargetPosition.z),
                    FormatFloat(result.MovementTimeSeconds),
                    result.Hit ? "1" : "0",
                    FormatFloat(definition.IndexOfDifficulty),
                    FormatFloat(result.ThroughputBitsPerSecond),
                    EscapeCsv(result.FailureReason)));
            }

            File.WriteAllText(_rawCsvPath, rawBuilder.ToString());

            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine("method,amplitude_m,diameter_m,mean_movement_time_s,mean_throughput_bits_s,error_rate_percent,shannon_id");

            var byMethod = _results.GroupBy(r => r.Definition.Method);

            foreach (var methodGroup in byMethod)
            {
                var byCondition = methodGroup.GroupBy(r => new
                {
                    r.Definition.AmplitudeMeters,
                    r.Definition.DiameterMeters
                });

                foreach (var conditionGroup in byCondition)
                {
                    var all = conditionGroup.ToList();
                    var hits = all.Where(r => r.Hit).ToList();

                    float meanMt = hits.Count > 0
                        ? hits.Average(r => r.MovementTimeSeconds)
                        : float.NaN;

                    float meanTp = hits.Count > 0
                        ? hits.Average(r => r.ThroughputBitsPerSecond)
                        : float.NaN;

                    float total = all.Count;
                    float misses = all.Count(r => !r.Hit);
                    float errorRate = total > 0 ? (100f * misses / total) : 0f;

                    float id = all.First().Definition.IndexOfDifficulty;

                    summaryBuilder.AppendLine(string.Join(",",
                        FormatMethod(methodGroup.Key),
                        FormatFloat(conditionGroup.Key.AmplitudeMeters),
                        FormatFloat(conditionGroup.Key.DiameterMeters),
                        FormatFloat(meanMt),
                        FormatFloat(meanTp),
                        FormatFloat(errorRate),
                        FormatFloat(id)));
                }
            }

            File.WriteAllText(_summaryCsvPath, summaryBuilder.ToString());
        }

        private void ComputeRegression(List<TrialResult> hits, out float intercept, out float slope, out bool valid)
        {
            intercept = float.NaN;
            slope = float.NaN;
            valid = false;

            if (hits == null || hits.Count < 2)
                return;

            float meanX = hits.Average(r => r.Definition.IndexOfDifficulty);
            float meanY = hits.Average(r => r.MovementTimeSeconds);

            float numerator = 0f;
            float denominator = 0f;

            foreach (var r in hits)
            {
                float x = r.Definition.IndexOfDifficulty - meanX;
                float y = r.MovementTimeSeconds - meanY;

                numerator += x * y;
                denominator += x * x;
            }

            if (Mathf.Approximately(denominator, 0f))
            {
                valid = false;
                return;
            }

            slope = numerator / denominator;
            intercept = meanY - slope * meanX;
            valid = true;
        }

        private Vector3 ComputeTargetPosition(TrialDefinition trial, Vector3 anchor)
        {

            Vector3 yawed = Quaternion.AngleAxis(trial.DirectionDegrees.x, Vector3.up) * _mainCamera.transform.forward;
            Vector3 pitched = Quaternion.AngleAxis(-trial.DirectionDegrees.y, _mainCamera.transform.right) * yawed;

            return anchor + pitched.normalized * trial.AmplitudeMeters;
        }

        private Vector3 GetStartPoint()
        {
            return _fixedStartPoint;
        }

        private Ray GetPointerRay(InteractionMethod method)
        {
            if ((method == InteractionMethod.ControllerTriggerRay ||
                method == InteractionMethod.ControllerGripSelect) &&
                TryGetXRRay(XRNode.RightHand, out Ray ray))
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

        private bool IsControllerSelectPressed()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
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

        private bool IsGripPressed()
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed))
                return gripPressed;

            if (device.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
                return gripValue > 0.7f;

            return false;
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

        private static Text CreateText(Transform parent, Vector2 anchoredPosition, int fontSize, TextAnchor anchor)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = anchor;

            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(460f, 50f);
            rect.anchoredPosition = anchoredPosition;
            return text;
        }

        private static string FormatMethod(InteractionMethod method)
        {
            switch (method)
            {
                case InteractionMethod.ControllerTriggerRay:
                    return "Controller Trigger Ray";
                case InteractionMethod.ControllerGripSelect:
                    return "Controller Grip Select";
                case InteractionMethod.HeadGazeDwell:
                    return "Head Gaze Dwell";
                default:
                    return method.ToString();
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