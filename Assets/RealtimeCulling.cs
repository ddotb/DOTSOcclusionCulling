using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using System;
using Unity.Mathematics;
using Random = UnityEngine.Random;

public class RealtimeCulling : MonoBehaviour
{
    [SerializeField] private Camera m_Camera;
    [SerializeField] private Spawner m_ObjectSpawner;
    [SerializeField] private NativeBSPTree m_BSPTree;

    [SerializeField] private AnimationCurve m_HorizontalBiasCurve;
    [SerializeField] private AnimationCurve m_VerticalBiasCurve;

    [SerializeField, Range(8, 128)] private int m_HorizontalPoints = 8;
    [SerializeField, Range(8, 64)] private int m_VerticalPoints = 8;
    [SerializeField, Range(8, 2048)] private int m_MaxObjects = 512;
    [SerializeField, Range(1, 16)] private int m_BatchingAmount = 16;
    [SerializeField, Range(1, 8)] private int m_MaxHits = 1;
    [SerializeField, Range(0, 1)] private float m_NoiseStrength = 0;

    [SerializeField] private int m_MaxBSPDepth = 5;

    [SerializeField]
    private bool m_ShowRaycasts;

    private Ray[] m_ScreenPointRays;
    private List<CullableObject> m_RegisteredCullables;

    private NativeArray<RaycastHit> m_RaycastResults;
    private NativeArray<RaycastCommand> m_RaycastCommands;
    private NativeArray<bool> m_ResultsFlags;
    private NativeArray<int> m_CullableIDs;
    private NativeArray<int> m_HitIDs;

    private ResultsJob m_ResultsJob;
    private JobHandle m_RaycastJobHandle;

    private int m_ScreenPointsTotal;

    public NativeArray<bool> ResultsFlags { get => m_ResultsFlags; }

    private void Start()
    {
        m_RegisteredCullables = new List<CullableObject>(m_MaxObjects);

        BuildScreenRays();

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

        BuildBSPTree();
        BuildScreenRays();

        //Populate job data - could this be optimised? Possible unbreakable reliance on the camera position
        //This is taking 20% of the total feature time now
        for (int i = 0; i < m_ScreenPointsTotal; i++)
        {
            Ray ray = m_ScreenPointRays[i];
            command.from = ray.origin;
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
        //Why is there no good way to do this?
        //Could memcpy into unsafe equivalent container and do parallel check on that
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

    private void BuildBSPTree()
    {
        NativeArray<BSPObject> objects = new NativeArray<BSPObject>(m_RegisteredCullables.Count, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        BSPObject newObject = new BSPObject();

        for (int i = 0; i < objects.Length; i++)
        {
            newObject.ID = i;
            newObject.Position = new float3(m_RegisteredCullables[i].transform.position);

            objects[i] = newObject;
        }

        m_BSPTree.Populate(objects, m_MaxBSPDepth);
    }

    private void BuildScreenRays()
    {
        m_ScreenPointsTotal = m_HorizontalPoints * m_VerticalPoints;
        m_ScreenPointRays = new Ray[m_ScreenPointsTotal];

        Vector3 separationUnit = new Vector3((float)Screen.height / m_VerticalPoints, (float)Screen.width / m_HorizontalPoints);

        Vector3 screenRayPosition;

        //TODO: Smarter points placement
        //TODO: add line of points along screen mid-height
        //Get roughly-evenly-spaced points across the frustum
        int index = 0;
        for (int i = 0; i < m_VerticalPoints; i++)
        {
            for (int j = 0; j < m_HorizontalPoints; j++)
            {
                Vector3 screenPosition = new Vector3(j * (Screen.width / m_HorizontalPoints), i * (Screen.height / m_VerticalPoints)) + (Vector3)Random.insideUnitCircle * m_NoiseStrength;

                //TODO: Factor in bias strength
                float xPos = m_HorizontalBiasCurve.Evaluate(screenPosition.x / Screen.width) * Screen.width;
                float yPos = m_VerticalBiasCurve.Evaluate(screenPosition.y / Screen.height) * Screen.height;

                screenRayPosition = new Vector3(xPos, yPos);

                screenRayPosition += (Vector3)Random.insideUnitCircle * m_NoiseStrength;

                m_ScreenPointRays[index] = m_Camera.ScreenPointToRay(screenRayPosition);

                //Use index as we're storing in a 1D array
                index++;
            }
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

    private void OnDrawGizmos()
    {
        if (m_RegisteredCullables != null)
        {
            Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.05f);
            for (int i = 0; i < m_RegisteredCullables.Count; i++)
            {
                Gizmos.DrawWireCube(m_RegisteredCullables[i].transform.position, m_RegisteredCullables[i].transform.lossyScale);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (m_ShowRaycasts)
        {
            if (m_ScreenPointRays != null)
            {
                for (int i = 0; i < m_ScreenPointsTotal; i++)
                {
                    Gizmos.color = new Color((float)i / m_ScreenPointsTotal, (float)i / m_ScreenPointsTotal, (float)i / m_ScreenPointsTotal, 0.25f);
                    Gizmos.DrawRay(m_ScreenPointRays[i].origin, m_ScreenPointRays[i].direction * m_Camera.farClipPlane);
                }
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
