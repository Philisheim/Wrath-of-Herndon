using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Herndon : MonoBehaviour
{
    // ======================================================
    // ================ ENUM & VARIABLES ==================
    // ======================================================
    public enum EnemyState { Roaming, Chasing, Investigating, Searching, Enraged }
    private EnemyState currentState;

    private NavMeshAgent agent;
    private Transform player;

    // -------- Movement Settings --------
    [Header("Movement Speeds")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.0f;

    // -------- Detection Settings --------
    [Header("Detection Settings")]
    public float sightRange = 15f;
    public float hearingRange = 25f;
    // Rage-modified values (base + increase)
    private float rageSight;
    private float rageHearing;
    private float rageWalk;
    private float rageRun;
    public float rageSightIncrease;   // Extra added to sightRange when enraged
    public float rageHearingIncrease;
    public float rageWalkIncrease;
    public float rageRunIncrease;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    // -------- Rage Settings --------
    [Header("Rage Settings")]
    public float maxRage = 100f;
    public float rageIncreaseRate = 10f; // Per second when player is visible
    public float rageDecreaseRate = 5f;  // Per second (after 4-second delay)
    public float currentRage = 0f;
    private bool isEnraged = false;
    private float rageCooldownTimer = 0f;
    private bool inRageCooldown = false;
    private float outOfSightTimer = 0f;
    public float rageDecreaseMultiplier = 1f;

    // -------- Nudge Cooldown --------
    private float nudgeCooldownTimer = 0f;
    private Vector3 lastNudgeTarget;

    // -------- Roaming & Searching --------
    [Header("Roam settings")]
    private Vector3 roamTarget;
    private List<Vector3> visitedLocations = new List<Vector3>();
    private List<Vector3> navMeshPoints = new List<Vector3>(); // Precomputed points
    private float roamTimer = 0f;
    public float roamInterval = 30f;

    // -------- Investigating --------
    private Vector3 investigateTarget;
    private bool hasInvestigateTarget = false;

    // -------- Searching --------
    [Header("Search settings")]
    private Vector3 lastSeenPlayerPos;
    private bool isSearching = false;
    public float searchTime = 15f; // Duration of search state
    private float searchTimer = 0f;

    // -------- Original Values --------
    private float originalWalkSpeed;
    private float originalRunSpeed;
    private float originalSightRange;
    private float originalHearingRange;

    // ======================================================
    // ===================== INITIALIZATION =================
    // ======================================================
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        currentState = EnemyState.Roaming;
        SetNewRoamDestination();

        // Cache original movement and detection values.
        originalWalkSpeed = walkSpeed;
        originalRunSpeed = runSpeed;
        originalSightRange = sightRange;
        originalHearingRange = hearingRange;

        // Calculate rage-modified values.
        rageSight = sightRange + rageSightIncrease;
        rageHearing = hearingRange + rageHearingIncrease;
        rageWalk = walkSpeed + rageWalkIncrease;
        rageRun = runSpeed + rageRunIncrease;

        PrecomputeNavMeshPoints();
    }

    void Update()
    {
        nudgeCooldownTimer = Mathf.Max(nudgeCooldownTimer - Time.deltaTime, 0f);

        roamTimer += Time.deltaTime;
        if (roamTimer >= roamInterval && !isEnraged)
        {
            roamTimer = 0f;
            SetNewRoamDestination();
        }

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
        ClampSpeed();
    }

    // ======================================================
    // =================== UTILITY METHODS ==================
    // ======================================================
    void ClampSpeed()
    {
        if (currentState == EnemyState.Roaming || currentState == EnemyState.Searching || currentState == EnemyState.Investigating)
            agent.speed = isEnraged ? rageWalk : originalWalkSpeed;
        else if (currentState == EnemyState.Chasing || currentState == EnemyState.Enraged)
            agent.speed = isEnraged ? rageRun : originalRunSpeed;
    }

    void PrecomputeNavMeshPoints()
    {
        NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();
        foreach (Vector3 vertex in navMeshData.vertices)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(vertex, out hit, 1f, NavMesh.AllAreas))
                navMeshPoints.Add(hit.position);
        }
    }

    Vector3 GetRandomNavMeshLocation()
    {
        if (navMeshPoints.Count == 0)
            return transform.position;
        int randomIndex = Random.Range(0, navMeshPoints.Count);
        Vector3 chosenPoint = navMeshPoints[randomIndex];
        if (!visitedLocations.Contains(chosenPoint))
            return chosenPoint;
        return transform.position;
    }

    // ======================================================
    // ================ DETECTION METHODS ===================
    // ======================================================
    void CheckForPlayer()
    {
        if (player == null)
            return;

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
            outOfSightTimer = 0f;
            if (!isEnraged)
            {
                isSearching = false;
                searchTimer = 0f;
                currentState = EnemyState.Chasing;
            }
        }
        else if (currentState == EnemyState.Chasing && !isEnraged)
        {
            StartSearching();
        }
    }

    bool CanSeePlayer()
    {
        if (player == null)
            return false;
        Collider[] hits = Physics.OverlapSphere(transform.position, sightRange, playerLayer);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position, directionToPlayer, Vector3.Distance(transform.position, player.position), obstacleLayer))
                    return true;
            }
        }
        return false;
    }

    // ======================================================
    // ================= MOVEMENT METHODS ===================
    // ======================================================
    void Roam()
    {
        agent.speed = walkSpeed;
        if (Vector3.Distance(transform.position, roamTarget) < 2f)
            SetNewRoamDestination();
    }

    void SetNewRoamDestination()
    {
        if (nudgeCooldownTimer > 0f)
            return;
        roamTarget = GetRandomNavMeshLocation();
        agent.SetDestination(roamTarget);
        if (Vector3.Distance(transform.position, roamTarget) > 2f)
        {
            visitedLocations.Add(roamTarget);
            if (visitedLocations.Count > 10)
                visitedLocations.RemoveAt(0);
        }
        nudgeCooldownTimer = 1f;
    }

    void ChasePlayer()
    {
        agent.speed = runSpeed;
        if (player != null)
        {
            NavMeshPath path = new NavMeshPath();
            agent.CalculatePath(player.position, path);
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                agent.SetDestination(player.position);
            }
            else
            {
                StartSearching();
            }
        }
        if (Vector3.Distance(transform.position, lastSeenPlayerPos) < 2f && !isEnraged)
            StartSearching();
    }

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

    void StartSearching()
    {
        if (!isSearching && !isEnraged)
        {
            isSearching = true;
            searchTimer = searchTime;
            currentState = EnemyState.Searching;
            SetNewSearchDestination();
        }
    }

    // Gets a valid destination within 5 meters of the last seen position.
    Vector3 GetFallbackDestination(Vector3 lastKnownPos)
    {
        Vector3 randomOffset = Random.insideUnitSphere * 5f;
        Vector3 target = lastKnownPos + randomOffset;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 2f, NavMesh.AllAreas))
            return hit.position;
        return lastKnownPos;
    }

    void SetNewSearchDestination()
    {
        Vector3 fallbackDestination = GetFallbackDestination(lastSeenPlayerPos);
        agent.SetDestination(fallbackDestination);
    }

    void SearchArea()
    {
        if (player == null)
            return;
        if (!agent.pathPending && agent.remainingDistance <= 2f)
        {
            SetNewSearchDestination();
        }
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            currentState = EnemyState.Roaming;
            isSearching = false;
            searchTimer = 0f;
        }
    }

    // ======================================================
    // ===================== RAGE SYSTEM ====================
    // ======================================================
    void HandleRage()
    {
        if (CanSeePlayer())
        {
            outOfSightTimer = 0f;
            currentRage += Time.deltaTime * rageIncreaseRate;
        }
        else
        {
            outOfSightTimer += Time.deltaTime;
            if (outOfSightTimer >= 4f)
                currentRage -= Time.deltaTime * rageDecreaseRate * rageDecreaseMultiplier;
        }
        currentRage = Mathf.Clamp(currentRage, 0, maxRage);

        if (!inRageCooldown && currentRage >= maxRage)
        {
            currentRage = maxRage;
            isEnraged = true;
            currentState = EnemyState.Enraged;
        }
        else if (isEnraged && currentRage < 0.7f * maxRage && !inRageCooldown)
        {
            isEnraged = false;
            inRageCooldown = true;
            rageCooldownTimer = 5f;
            currentState = EnemyState.Chasing;
        }
        if (inRageCooldown)
        {
            rageCooldownTimer -= Time.deltaTime;
            if (rageCooldownTimer <= 0f)
            {
                inRageCooldown = false;
                currentState = CanSeePlayer() ? EnemyState.Chasing : EnemyState.Searching;
            }
        }
    }

    void EnragedBehavior()
    {
        runSpeed = rageRun;
        walkSpeed = rageWalk;
        sightRange = rageSight;
        hearingRange = rageHearing;
        if (player != null)
        {
            if (CanSeePlayer())
            {
                // Chase the player if visible.
                agent.SetDestination(player.position);
            }
            else
            {
                // Use nudge as the primary method while enraged.
                NudgeTowardsPlayer();
            }
        }
    }

    void NormalBehavior()
    {
        walkSpeed = originalWalkSpeed;
        runSpeed = originalRunSpeed;
        sightRange = originalSightRange;
        hearingRange = originalHearingRange;
    }

    /// <summary>
    /// Nudges the enemy toward a target point 45 meters from the player's position.
    /// Adds a random offset if the computed target is nearly identical to the previous one.
    /// </summary>
    void NudgeTowardsPlayer()
    {
        if (player == null)
            return;
        if (nudgeCooldownTimer > 0f)
            return;
        Vector3 direction = (transform.position - player.position).normalized;
        Vector3 targetPosition = player.position + direction * 45f;
        if (Vector3.Distance(targetPosition, lastNudgeTarget) < 1f)
            targetPosition += Random.insideUnitSphere * 2f;
        if (Vector3.Distance(targetPosition, agent.destination) < 2f)
            targetPosition += Random.insideUnitSphere * 2f;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            lastNudgeTarget = hit.position;
        }
        else
        {
            agent.SetDestination(lastSeenPlayerPos);
        }
        nudgeCooldownTimer = 3f;
    }
}
