using System;
using System.Collections.Generic;
using UnityEngine;

namespace HW2.FittsLaw
{
    public enum InteractionMethod
    {
        ControllerTriggerRay,
        ControllerGripSelect,
        HeadGazeDwell
    }

    [Serializable]
    public sealed class TrialDefinition
    {
        public int TrialNumber;
        public int Repetition;
        public InteractionMethod Method;
        public float AmplitudeMeters;
        public float DiameterMeters;
        public Vector2 DirectionDegrees;

        public float IndexOfDifficulty
        {
            get { return Mathf.Log((AmplitudeMeters / DiameterMeters) + 1f, 2f); }
        }
    }

    public sealed class TrialResult
    {
        public TrialDefinition Definition;
        public float MovementTimeSeconds;
        public bool Hit;
        public Vector3 StartPoint;
        public Vector3 TargetPosition;
        public string FailureReason;

        public float ThroughputBitsPerSecond
        {
            get
            {
                if (!Hit || MovementTimeSeconds <= 0f)
                {
                    return 0f;
                }

                return Definition.IndexOfDifficulty / MovementTimeSeconds;
            }
        }
    }

    [Serializable]
    public sealed class ExperimentConfig
    {
        public string GroupName = "OregonShooters";
        public int ParticipantIndex = 0;
        public float[] DistancesMeters = { 0.5f, 1.6f };  // maxine put .35-.55
        public float[] DiametersMeters = { 0.08f, 0.16f };  // maxine put .08-.12
        public Vector2[] DirectionsDegrees =
        {
            new Vector2(-20f, 8f),
            new Vector2(20f, 8f),
            new Vector2(-12f, -6f),
            new Vector2(12f, -6f)
        };
        public int Repetitions = 2;
        public float TrialTimeoutSeconds = 4f;
        public float GazeDwellSeconds = 1.1f;

        public InteractionMethod[] Methods =
        {
            InteractionMethod.ControllerTriggerRay,
            InteractionMethod.ControllerGripSelect,
            InteractionMethod.HeadGazeDwell
        };

        public List<TrialDefinition> BuildTrials()
        {
            var trials = new List<TrialDefinition>();
            int trialNumber = 1;

            var orderedMethods = BuildLatinSquareOrder();

            var conditions = new List<(InteractionMethod method, float dist, float size)>();

            foreach (var method in orderedMethods)
            {
                foreach (var d in DistancesMeters)
                {
                    foreach (var s in DiametersMeters)
                    {
                        conditions.Add((method, d, s));
                    }
                }
            }

            for (int rep = 0; rep < Repetitions; rep++)
            {
                int shift = rep % conditions.Count;

                for (int i = 0; i < conditions.Count; i++)
                {
                    var c = conditions[(i + shift) % conditions.Count];

                    int directionIndex = (i + rep) % DirectionsDegrees.Length;

                    trials.Add(new TrialDefinition
                    {
                        TrialNumber = trialNumber++,
                        Repetition = rep + 1,
                        Method = c.method,
                        AmplitudeMeters = c.dist,
                        DiameterMeters = c.size,
                        DirectionDegrees = DirectionsDegrees[directionIndex]
                    });
                }
            }

          return trials;
        }

        private List<InteractionMethod> BuildLatinSquareOrder()
        {
            var methods = new List<InteractionMethod>(Methods);
            if (methods.Count <= 1)
            {
                return methods;
            }

            int rowIndex = Mathf.Abs(ParticipantIndex) % methods.Count;
            var ordered = new List<InteractionMethod>(methods.Count);

            for (int i = 0; i < methods.Count; i++)
            {
                int offset;
                if (i % 2 == 0)
                {
                    offset = i / 2;
                }
                else
                {
                    offset = methods.Count - ((i + 1) / 2);
                }

                int index = (rowIndex + offset) % methods.Count;
                ordered.Add(methods[index]);
            }

            return ordered;
        }
    }
}