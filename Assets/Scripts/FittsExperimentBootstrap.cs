using UnityEngine;
using UnityEngine.SceneManagement;

namespace HW2.FittsLaw
{
    public static class FittsExperimentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateManager()
        {
            if (Object.FindFirstObjectByType<FittsExperimentManager>() != null)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            var root = new GameObject("FittsExperimentRuntime");
            root.AddComponent<FittsExperimentManager>();
        }
    }
}
