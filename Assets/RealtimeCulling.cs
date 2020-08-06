using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

public class RealtimeCulling : MonoBehaviour
{
    [SerializeField] private Camera m_Camera;
    [SerializeField] private Spawner m_ObjectSpawner;

    [SerializeField, Range(100, 100000)] private int m_ScreenPointsTotal = 2048;
    [SerializeField, Range(8, 2048)] private int m_MaxObjects = 512;
    [SerializeField, Range(1, 16)] private int m_BatchingAmount = 16;
    [SerializeField, Range(1, 8)] private int m_MaxHits = 1;
    [SerializeField, Range(0, 25)] private float m_NoiseStrength = 0;

    private Ray[] m_ScreenPointRays;
    private List<CullableObject> m_RegisteredCullables;

    private NativeArray<RaycastHit> m_RaycastResults;
    private NativeArray<RaycastCommand> m_RaycastCommands;
    private NativeArray<bool> m_ResultsFlags;
    private NativeArray<int> m_CullableIDs;
    private NativeArray<int> m_HitIDs;

    private ResultsJob m_ResultsJob;
    private JobHandle m_RaycastJobHandle;

    public NativeArray<bool> ResultsFlags { get => m_ResultsFlags; }

    private void Start()
    {
        m_RegisteredCullables = new List<CullableObject>(m_MaxObjects);

        float pointsRoot = Mathf.Sqrt(m_ScreenPointsTotal);
        float aspectRatio = m_Camera.aspect;

        Vector3 separationUnit = new Vector3((float)Screen.height / (pointsRoot / aspectRatio), (float)Screen.width / (pointsRoot * aspectRatio));

        m_ScreenPointRays = new Ray[m_ScreenPointsTotal];

        Vector3 screenRayPosition;

        //TODO: Smarter points placement
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
        m_HitIDs = new NativeArray<int>(m_ScreenPointsTotal, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        //Set up our objects arrays using our defined maximum
        m_CullableIDs = new NativeArray<int>(m_MaxObjects, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        m_ResultsFlags = new NativeArray<bool>(m_MaxObjects, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        m_ResultsJob = new ResultsJob
        {
            Hits = m_HitIDs,
            Cullables = m_CullableIDs,
            Results = m_ResultsFlags
        };
    }

    private void Update()
    {
        RaycastCommand command = new RaycastCommand();

        //Populate job data - could this be optimised? Possible unbreakable reliance on the camera position
        //This is taking 20% of the total feature time now
        for (int i = 0; i < m_ScreenPointsTotal; i++)
        {
            Ray ray = m_ScreenPointRays[i];
            command.from = ray.origin + m_Camera.transform.position;
            command.direction = ray.direction;
            command.distance = m_Camera.farClipPlane;
            command.maxHits = m_MaxHits;
            command.layerMask = ~0;
            m_RaycastCommands[i] = command;
        }

        //Fire off job
        m_RaycastJobHandle = RaycastCommand.ScheduleBatch(m_RaycastCommands, m_RaycastResults, m_BatchingAmount);

        //Make sure job is complete
        m_RaycastJobHandle.Complete();

        //TODO: Jobify arranging hit IDs
        //This is taking 45% of the total feature time now
        for (int i = 0; i < m_ScreenPointsTotal; i++)
        {
            Collider collider = m_RaycastResults[i].collider;

            if (collider != null)
            {
                m_HitIDs[i] = collider.GetInstanceID();
            }
        }

        m_ResultsJob.Schedule(m_MaxObjects, m_BatchingAmount).Complete();

        //Update all registered objects - Unsure if this is optimisable
        //This is taking up 35% of the total feature time now
        for (int i = 0; i < m_RegisteredCullables.Count; i++)
        {
            m_RegisteredCullables[i].SetRenderState(m_ResultsFlags[i]);
        }
    }

    private void OnDestroy()
    {
        m_ResultsFlags.Dispose();
        m_RaycastResults.Dispose();
        m_RaycastCommands.Dispose();

        m_CullableIDs.Dispose();
        m_HitIDs.Dispose();
    }

    public void Register(CullableObject objectToAdd)
    {
        //TODO: Replace with ID requester
        int objectID = m_RegisteredCullables.Count;

        if (objectID < m_MaxObjects)
        {
            m_RegisteredCullables.Add(objectToAdd);

            //TODO: Enable reuse of IDs
            m_CullableIDs[objectID] = m_RegisteredCullables[objectID].Identifier;
        }
        else
        {
            Debug.LogError("[Culling] Cannot add new object as the limit has been reached");
        }
    }

    public void Deregister(CullableObject objectToRemove)
    {
        m_RegisteredCullables.Remove(objectToRemove);
    }

    private void OnDrawGizmosSelected()
    {
        if (m_ScreenPointRays != null)
        {
            for (int i = 0; i < m_ScreenPointsTotal; i++)
            {
                Gizmos.color = new Color((float)i / m_ScreenPointsTotal, (float)i / m_ScreenPointsTotal, (float)i / m_ScreenPointsTotal);
                Gizmos.DrawRay(m_ScreenPointRays[i].origin + m_Camera.transform.position, m_ScreenPointRays[i].direction * m_Camera.farClipPlane);
            }
        }
    }

    [BurstCompile]
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
