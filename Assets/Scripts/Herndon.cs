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
    public float rageSightIncrease = 10f;
    public float rageHearingIncrease = 15f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Rage Settings")]
    public float maxRage = 100f;
    public float rageDecreaseRate = 5f;
    private float currentRage = 0f;
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

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, sightRange, playerLayer | obstacleLayer))
        {
            if (hit.collider.CompareTag("Player"))
            {
                lastSeenPlayerPos = player.position;
                isSearching = false;
                searchTimer = 0f;
                currentState = EnemyState.Chasing;
            }
        }
        else if (currentState == EnemyState.Chasing)
        {
            StartSearching();
        }
    }

    void ChasePlayer()
    {
        agent.speed = runSpeed;

        // If the player is still present, continue chasing
        if (player != null)
        {
            agent.SetDestination(player.position);

            // Increase rage over time while chasing
            AddRage(20f * Time.deltaTime); // Increase rage by 20 per second
        }
    }


    // ---------------- Searching Logic ----------------
    void StartSearching()
    {
        isSearching = true;
        searchTimer = 0f;
        currentState = EnemyState.Searching;
        SetNewSearchDestination();
    }

    void SearchArea()
    {
        agent.speed = walkSpeed;

        // If enemy reaches the search target, pick a new one
        if (Vector3.Distance(transform.position, roamTarget) < 2f)
        {
            SetNewSearchDestination();
        }

        searchTimer += Time.deltaTime;

        // If the timer runs out, go back to roaming
        if (searchTimer >= searchTime)
        {
            isSearching = false;
            searchTimer = 0f;
            currentState = EnemyState.Roaming;
            SetNewRoamDestination();
        }

        // If the enemy sees the player while searching, switch to chasing
        if (player != null)
        {
            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, sightRange, playerLayer | obstacleLayer))
            {
                if (hit.collider.CompareTag("Player"))
                {
                    lastSeenPlayerPos = player.position;
                    isSearching = false;
                    searchTimer = 0f;
                    currentState = EnemyState.Chasing;
                }
            }
        }
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
        currentRage += amount;
        // If rage reaches 100%, apply the enraged state
        if (currentRage >= maxRage)
        {
            EnterEnragedState();
        }
    }

    public void ReduceRage(float amount)
    {
        currentRage = Mathf.Max(0, currentRage - amount);
        if (currentRage < maxRage && isEnraged)
        {
            ExitEnragedState();
        }
    }

    void EnterEnragedState()
    {
        isEnraged = true;
        sightRange += rageSightIncrease;
        hearingRange += rageHearingIncrease;
        walkSpeed += 2.5f;
        runSpeed += 2.5f;
        currentState = EnemyState.Enraged;
    }

    void ExitEnragedState()
    {
        isEnraged = false;
        sightRange -= rageSightIncrease;
        hearingRange -= rageHearingIncrease;
        walkSpeed -= 2.5f;
        runSpeed -= 2.5f;
        currentState = EnemyState.Roaming;
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
