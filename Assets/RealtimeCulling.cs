using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class RealtimeCulling : MonoBehaviour
{
    [SerializeField] private Camera m_Camera;
    [SerializeField] private Spawner m_ObjectSpawner;

    [SerializeField, Range(100, 100000)] private int m_ScreenPointsTotal;
    [SerializeField, Range(1, 16)] private int m_BatchingAmount;
    [SerializeField, Range(1, 8)] private int m_MaxHits;
    [SerializeField, Range(0, 25)] private float m_NoiseStrength;

    private Ray[] m_ScreenPointRays;
    private List<Cullable> m_Cullables;

    private NativeArray<RaycastHit> m_RaycastResults;
    private NativeArray<RaycastCommand> m_RaycastCommands;
    private NativeArray<bool> m_ResultsFlags;

    private JobHandle m_RaycastJob;

    private bool m_Active = false;

    public NativeArray<bool> ResultsFlags { get => m_ResultsFlags; }

    public bool Active { get => m_Active; }

    private void Start()
    {
        m_ObjectSpawner.OnSpawnFinished += OnSpawnFinished;

        float pointsRoot = Mathf.Sqrt(m_ScreenPointsTotal);
        float aspectRatio = m_Camera.aspect;

        Vector3 separationUnit = new Vector3((float)Screen.height / (pointsRoot / aspectRatio), (float)Screen.width / (pointsRoot * aspectRatio));

        m_ScreenPointRays = new Ray[m_ScreenPointsTotal];

        Vector3 screenRayPosition;

        //Get roughly-evenly-spaced points across the frustum
        int index = 0;
        for (int i = 1; i < pointsRoot; i++)
        {
            for (int j = 1; j < pointsRoot; j++)
            {
                screenRayPosition = new Vector3(j * separationUnit.x, i * separationUnit.y);

                screenRayPosition += (Vector3)Random.insideUnitCircle * m_NoiseStrength;

                m_ScreenPointRays[index] = m_Camera.ScreenPointToRay(screenRayPosition);

                //Use index as we're storing in a 1D array
                index++;
            }
        }

        //Set up native arrays for the jobs, clear memory
        m_RaycastResults = new NativeArray<RaycastHit>(m_ScreenPointsTotal, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        m_RaycastCommands = new NativeArray<RaycastCommand>(m_ScreenPointsTotal, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    private void Update()
    {
        if (!m_Active)
        {
            return;
        }

        RaycastCommand command = new RaycastCommand();

        //Populate job data
        for (int i = 0; i < m_ScreenPointsTotal; i++)
        {
            command.from = m_ScreenPointRays[i].origin + m_Camera.transform.position;
            command.direction = m_ScreenPointRays[i].direction;
            command.distance = m_Camera.farClipPlane;
            command.maxHits = m_MaxHits;
            command.layerMask = ~0;
            m_RaycastCommands[i] = command;
        }

        m_ResultsFlags = new NativeArray<bool>(m_Cullables.Count, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        //Fire off job
        m_RaycastJob = RaycastCommand.ScheduleBatch(m_RaycastCommands, m_RaycastResults, m_BatchingAmount);

        //Make sure job is complete
        m_RaycastJob.Complete();

        NativeArray<int> cullableIDs = new NativeArray<int>(m_Cullables.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < m_Cullables.Count; i++)
        {
            cullableIDs[i] = m_Cullables[i].Collider.GetInstanceID();
        }

        NativeArray<int> hitIDs = new NativeArray<int>(m_RaycastResults.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < m_RaycastResults.Length; i++)
        {
            if (m_RaycastResults[i].collider != null)
            {
                hitIDs[i] = m_RaycastResults[i].collider.GetInstanceID();
            }
        }

        ResultsJob resultsJob = new ResultsJob
        {
            Hits = hitIDs,
            Cullables = cullableIDs,
            Results = m_ResultsFlags
        };

        resultsJob.Schedule(cullableIDs.Length, m_BatchingAmount).Complete();

        cullableIDs.Dispose();
        hitIDs.Dispose();
    }

    private void OnDestroy()
    {
        m_ResultsFlags.Dispose();
        m_RaycastResults.Dispose();
        m_RaycastCommands.Dispose();
    }

    private void OnSpawnFinished(List<Cullable> cullables)
    {
        m_Active = true;

        m_Cullables = cullables;
    }

    private void OnDrawGizmosSelected()
    {
        if (!m_Active)
        {
            return;
        }

        for (int i = 0; i < m_ScreenPointRays.Length; i++)
        {
            Gizmos.DrawRay(m_ScreenPointRays[i].origin + m_Camera.transform.position, m_ScreenPointRays[i].direction * m_Camera.farClipPlane);
        }
    }

    private struct ResultsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int> Hits;
        [ReadOnly] public NativeArray<int> Cullables;
        [WriteOnly] public NativeArray<bool> Results;

        public void Execute(int index)
        {
            for (int i = 0; i < Hits.Length; i++)
            {
                if (Hits[i] == Cullables[index])
                {
                    Results[index] = true;

                    return;
                }
            }

            Results[index] = false;
        }
    }
}
