using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TrialManager : MonoBehaviour
{
    public GameObject targetPrefab;
    public Transform xrOrigin;
    public List<Vector3> trialPositions = new List<Vector3>();
    public List<float> trialSizes = new List<float>();
    public float hoverDistance = 0.05f; // how close the ray must be to count as hover

    private int currentTrial = 0;
    private GameObject currentTarget;
    private float trialStartTime;

    private string filePath;

    void Start()
    {
        filePath = Path.Combine(Application.dataPath, "YourGroupName_Outputfile.csv");
        File.WriteAllText(filePath, "Trial,Position,Size,TimeTaken,Hit\n");
        SpawnNextTarget();
    }

    void Update()
    {
        if (currentTarget == null) return;

        // Check for selection with ray interactor
        XRRayInteractor ray = xrOrigin.GetComponentInChildren<XRRayInteractor>();
        if (ray)
        {
            List<XRBaseInteractable> hits = new List<XRBaseInteractable>();
            if (ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                if (hit.collider.gameObject == currentTarget)
                {
                    // Hit detected
                    float timeTaken = Time.time - trialStartTime;
                    LogTrial(true, timeTaken);
                    Destroy(currentTarget);
                    SpawnNextTarget();
                }
            }
        }
    }

    void SpawnNextTarget()
    {
        if (currentTrial >= trialPositions.Count)
        {
            Debug.Log("All trials finished. CSV saved at: " + filePath);
            return;
        }

        Vector3 pos = trialPositions[currentTrial];
        float size = trialSizes[currentTrial];

        currentTarget = Instantiate(targetPrefab, pos, Quaternion.identity);
        currentTarget.transform.localScale = Vector3.one * size;

        trialStartTime = Time.time;
        currentTrial++;
    }

    void LogTrial(bool hit, float timeTaken)
    {
        string log = $"{currentTrial},{currentTarget.transform.position},{currentTarget.transform.localScale.x},{timeTaken},{hit}\n";
        File.AppendAllText(filePath, log);
    }
}