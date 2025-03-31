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
    public float rageSightIncrease;   // Extra sightRange when enraged
    public float rageHearingIncrease; // Extra hearing range while enraged
    public float rageWalkIncrease;    // Extra walk speed while enraged
    public float rageRunIncrease;     // Extra run speed while enraged
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
    [Tooltip("Percentage of max rage (default 45%) before rage buffs are lost")]
    public float rageThreshold = 0.45f; // Default 45%

    // -------- Nudge Settings --------
    [Header("Nudge Settings")]
    [Tooltip("Radius around the player from which a nudge target is chosen (default 25 meters).")]
    public float nudgeRadius = 25f;
    private float nudgeCooldownTimer = 0f;
    private Vector3 lastNudgeTarget;

    // -------- Roaming & Searching --------
    private Vector3 roamTarget;
    private List<Vector3> visitedLocations = new List<Vector3>();
    private List<Vector3> navMeshPoints = new List<Vector3>(); // Precomputed points
    [Header("Roam settings")]
    [Tooltip("Maximum time (in seconds) Herndon can spend trying to roam before changing roam destinations")]
    public float roamInterval = 90f;
    public float roamTimer = 0;

    // -------- Investigating --------
    private Vector3 investigateTarget;
    private bool hasInvestigateTarget = false;

    // -------- Searching --------
    [Header("Search settings")]
    [Tooltip("Radius (in meters) Herndon can search in after losing sight of the player")]
    public float searchRadius = 10f;
    private Vector3 lastSeenPlayerPos;
    private bool isSearching = false;

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
        // When enraged and above threshold, use buffed speeds.
        // Otherwise, revert to original speeds.
        if (isEnraged && currentRage >= rageThreshold * maxRage)
        {
            if (currentState == EnemyState.Roaming || currentState == EnemyState.Searching || currentState == EnemyState.Investigating)
                agent.speed = rageWalk;
            else if (currentState == EnemyState.Chasing || currentState == EnemyState.Enraged)
                agent.speed = rageRun;
        }
        else
        {
            agent.speed = (currentState == EnemyState.Chasing || currentState == EnemyState.Enraged) ? originalRunSpeed : originalWalkSpeed;
        }
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

    // Gets a valid destination within the searchRadius of the last seen position.
    Vector3 GetFallbackDestination(Vector3 lastKnownPos)
    {
        int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * searchRadius;
            randomOffset.y = 0;
            Vector3 potentialTarget = lastKnownPos + randomOffset;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(potentialTarget, out hit, 5f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        // Fallback if all attempts fail.
        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(lastKnownPos, out fallbackHit, searchRadius, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }

        return lastKnownPos;
    }

    void StartSearching()
    {
        if (!isSearching && !isEnraged)
        {
            isSearching = true;
            currentState = EnemyState.Searching;
            SetNewSearchDestination();
        }
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
        else
        {
            currentState = EnemyState.Roaming;
            isSearching = false;
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

        // Rage state transitions:
        if (!inRageCooldown)
        {
            // Enter enraged state when rage reaches maximum.
            if (currentRage >= maxRage)
            {
                currentRage = maxRage;
                isEnraged = true;
                currentState = EnemyState.Enraged;
            }
            // Exit enraged state once rage falls below the threshold.
            else if (isEnraged && currentRage < rageThreshold * maxRage)
            {
                isEnraged = false;
                inRageCooldown = true;
                rageCooldownTimer = 5f;
                currentState = CanSeePlayer() ? EnemyState.Chasing : EnemyState.Searching;
            }
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

        // Ensure a valid state is set if none of the above conditions apply.
        if (currentState != EnemyState.Roaming &&
            currentState != EnemyState.Chasing &&
            currentState != EnemyState.Investigating &&
            currentState != EnemyState.Searching &&
            currentState != EnemyState.Enraged)
        {
            currentState = CanSeePlayer() ? EnemyState.Chasing : EnemyState.Roaming;
        }
    }

    void EnragedBehavior()
    {
        // If the enemy's rage is above the threshold, maintain buffed speeds.
        if (currentRage >= rageThreshold * maxRage)
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
                    // Every 2 seconds, nudge the enemy if the cooldown has elapsed.
                    if (nudgeCooldownTimer <= 0f)
                    {
                        NudgeTowardsPlayer();
                        nudgeCooldownTimer = 2f; // Reset the nudge timer to 2 seconds.
                    }
                }
            }
        }
        else
        {
            // Rage has fallen below the threshold, so remove buffs.
            NormalBehavior();
            currentState = CanSeePlayer() ? EnemyState.Chasing : EnemyState.Roaming;
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
    /// <summary>
    /// Nudges the enemy toward a target point that is closer to the player.
    /// The target is chosen from within a circle around the player (of radius nudgeRadius).
    /// If a valid target is not found after a few attempts, the enemy moves directly to the player.
    /// </summary>
    /// <summary>
    /// Nudges the enemy toward a target point that is closer to the player.
    /// The target is chosen from within a circle around the player (of radius nudgeRadius).
    /// If a valid target is not found after a few attempts, the enemy moves directly to the player.
    /// </summary>
    void NudgeTowardsPlayer()
    {
        if (player == null)
            return;

        // Calculate current distance from enemy to player.
        float currentDistance = Vector3.Distance(transform.position, player.position);

        // We want a target that's closer than currentDistance.
        Vector3 targetPosition = player.position; // Fallback
        bool foundValidTarget = false;
        int maxAttempts = 10;
        for (int i = 0; i < maxAttempts; i++)
        {
            // Pick a random point inside a circle of radius nudgeRadius (on XZ plane)
            Vector2 randomPoint = Random.insideUnitCircle * nudgeRadius;
            Vector3 potentialTarget = player.position + new Vector3(randomPoint.x, 0, randomPoint.y);

            // Ensure this point is closer to the player than the enemy's current position.
            if (Vector3.Distance(player.position, potentialTarget) < currentDistance)
            {
                targetPosition = potentialTarget;
                foundValidTarget = true;
                break;
            }
        }

        // If no valid target was found, default to the player's position.
        if (!foundValidTarget)
        {
            targetPosition = player.position;
        }

        // Validate the chosen target using NavMesh sampling.
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            lastNudgeTarget = hit.position;
        }
        else
        {
            // If sampling fails, set destination directly to the player.
            agent.SetDestination(player.position);
        }
    }
}
