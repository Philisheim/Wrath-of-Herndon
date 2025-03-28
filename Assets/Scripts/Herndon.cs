using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Herndon : MonoBehaviour
{
    public enum EnemyState { Roaming, Chasing, Investigating, Searching, Enraged }
    private EnemyState currentState;

    private NavMeshAgent agent;
    private Transform player;

    [Header("Movement Speeds")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.0f;

    [Header("Detection Settings")]
    public float sightRange = 15f;
    public float hearingRange = 25f;
    public float rageSight;
    public float rageHearing;
    public float rageWalk;
    public float rageRun;
    public float rageSightIncrease;
    public float rageHearingIncrease;
    public float rageWalkIncrease;
    public float rageRunIncrease;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Rage Settings")]
    public float maxRage = 100f;
    public float rageDecreaseRate = 5f;
    
    public float currentRage = 0f;
    private bool isEnraged = false;

    private Vector3 roamTarget;
    private Vector3 investigateTarget;
    private bool hasInvestigateTarget = false;

    private Vector3 lastSeenPlayerPos;
    private bool isSearching = false;
    private float searchTime = 5f;
    private float searchTimer = 0f;

    private float originalWalkSpeed;
    private float originalRunSpeed;
    private float originalSightRange;
    private float originalHearingRange;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        currentState = EnemyState.Roaming;
        SetNewRoamDestination();

        // Store the original values
        originalWalkSpeed = walkSpeed;
        originalRunSpeed = runSpeed;
        originalSightRange = sightRange;
        originalHearingRange = hearingRange;
        // Set the rage values
        rageSight = sightRange + rageSightIncrease;
        rageHearing = hearingRange + rageHearingIncrease;
        rageWalk = walkSpeed + rageWalkIncrease;
        rageRun = runSpeed + rageRunIncrease;
    }


    void Update()
    {
        switch (currentState)
        {
            case EnemyState.Roaming:
                Roam();
                break;
            case EnemyState.Chasing:
                ChasePlayer();
                break;
            case EnemyState.Investigating:
                Investigate();
                break;
            case EnemyState.Searching:
                SearchArea();
                break;
            case EnemyState.Enraged:
                EnragedBehavior();
                break;
        }

        CheckForPlayer();
        HandleRage();
    }

    // ---------------- Roaming Logic ----------------
    void Roam()
    {
        agent.speed = walkSpeed;
        if (Vector3.Distance(transform.position, roamTarget) < 2f)
        {
            SetNewRoamDestination();
        }
    }

    void SetNewRoamDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 10f;
        randomDirection += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
        {
            roamTarget = hit.position;
            agent.SetDestination(roamTarget);
        }
    }

    // ---------------- Chasing Logic ----------------
    void CheckForPlayer()
    {
        if (player == null) return;

        // Check if player is within sight range and not obstructed
        bool playerVisible = false;
        Collider[] hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position, directionToPlayer, Vector3.Distance(transform.position, player.position), obstacleLayer))
                {
                    playerVisible = true;
                    break;
                }
            }
        }

        if (playerVisible)
        {
            lastSeenPlayerPos = player.position;
            isSearching = false;
            searchTimer = 0f;
            currentState = EnemyState.Chasing;
        }
        else if (currentState == EnemyState.Chasing)
        {
            StartSearching();
        }
    }


    void ChasePlayer()
    {
        agent.speed = runSpeed;

        if (player != null)
        {
            agent.SetDestination(player.position);
            AddRage(20f * Time.deltaTime);
        }

        // If the enemy reaches the last known player position, start searching
        if (Vector3.Distance(transform.position, lastSeenPlayerPos) < 2f)
        {
            StartSearching();
        }
    }



    // ---------------- Searching Logic ----------------
    void StartSearching()
    {
        if (!isSearching) // Prevent unnecessary resets
        {
            isSearching = true;
            searchTimer = 0f;
            currentState = EnemyState.Searching;
            SetNewSearchDestination();
        }
    }


    void SearchArea()
    {
        agent.speed = walkSpeed;

        if (Vector3.Distance(transform.position, roamTarget) < 2f)
        {
            SetNewSearchDestination();
        }

        searchTimer += Time.deltaTime;

        if (searchTimer >= searchTime)
        {
            isSearching = false;
            searchTimer = 0f;
            currentState = EnemyState.Roaming;
            SetNewRoamDestination();
        }

        // Check if the player is now visible again
        CheckForPlayer();
    }



    void SetNewSearchDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 5f;
        randomDirection += lastSeenPlayerPos;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 5f, NavMesh.AllAreas))
        {
            roamTarget = hit.position;
            agent.SetDestination(roamTarget);
        }
    }

    // ---------------- Investigating Logic ----------------
    public void HearNoise(Vector3 noisePosition)
    {
        if (Vector3.Distance(transform.position, noisePosition) <= hearingRange)
        {
            investigateTarget = noisePosition;
            hasInvestigateTarget = true;
            currentState = EnemyState.Investigating;
        }
    }


    void Investigate()
    {
        agent.speed = walkSpeed;
        if (hasInvestigateTarget)
        {
            agent.SetDestination(investigateTarget);
            if (Vector3.Distance(transform.position, investigateTarget) < 2f)
            {
                hasInvestigateTarget = false;
                currentState = EnemyState.Roaming;
            }
        }
    }

    // ---------------- Rage Logic ----------------
    public void AddRage(float amount)
    {
        if(currentRage < maxRage)
        {
            currentRage += amount;
        }
        else if (currentRage > maxRage && !isEnraged)
        {
            currentRage = maxRage;
        }
        // If rage reaches 100%, apply the enraged state
        if (currentRage >= maxRage)
        {
            EnterEnragedState();
        }
    }

    public void ReduceRage(float amount)
    {
        currentRage = Mathf.Max(0, currentRage - amount);
        if (currentRage <= (0.75 * maxRage) && isEnraged)
        {
            ExitEnragedState();
        }
    }

    void EnterEnragedState()
    {
        isEnraged = true;
        sightRange = rageSight;
        hearingRange = rageHearing;
        walkSpeed = rageWalk;
        runSpeed = rageRun;
        currentState = EnemyState.Enraged;
    }

    void ExitEnragedState()
    {
        isEnraged = false;
        sightRange = originalSightRange;
        hearingRange = originalHearingRange;
        walkSpeed = originalWalkSpeed;
        runSpeed = originalRunSpeed;

        // If the player is still visible, chase; otherwise, return to normal behavior
        bool playerStillVisible = CanSeePlayer();

        if (playerStillVisible)
        {
            currentState = EnemyState.Chasing;
        }
        else
        {
            StartSearching(); // Start searching instead of immediately roaming
        }
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position, directionToPlayer, Vector3.Distance(transform.position, player.position), obstacleLayer))
                {
                    return true;
                }
            }
        }
        return false;
    }


    void EnragedBehavior()
    {
        if (player != null)
        {
            agent.SetDestination(player.position);
        }
    }

    void HandleRage()
    {
        if (currentRage > 0)
        {
            ReduceRage(rageDecreaseRate * Time.deltaTime);
        }

        // If rage falls below 75%, exit the enraged state
        if (currentRage < maxRage * 0.75f && isEnraged)
        {
            ExitEnragedState();
        }
    }

}
