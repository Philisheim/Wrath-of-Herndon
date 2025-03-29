using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Herndon : MonoBehaviour
{
    // ======================================================
    // ================ ENUM & VARIABLES ==================
    // ======================================================
    public enum EnemyState { Roaming, Chasing, Investigating, Searching, Enraged , Normal };
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
    // These values are used when enraged (base + increase)
    private float rageSight;
    private float rageHearing;
    private float rageWalk;
    private float rageRun;
    public float rageSightIncrease;   // Rate at which rage increases while player is visible
    public float rageHearingIncrease;
    public float rageWalkIncrease;
    public float rageRunIncrease;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    // -------- Rage Settings --------
    [Header("Rage Settings")]
    public float maxRage = 100f;
    public float rageIncreaseRate = 10f; // Rage increases per second when player is visible
    public float rageDecreaseRate = 5f; // Rage decreases per second after delay
    public float currentRage = 0f;
    private bool isEnraged = false;
    // Once enraged, if rage drops below 70% of max, a 10‑second cooldown is triggered.
    private float rageCooldownTimer = 0f;
    private bool inRageCooldown = false;
    // Timer to delay rage decrease until the player has been out of sight for 4 seconds.
    private float outOfSightTimer = 0f;
    public float rageDecreaseMultiplier; // Rate at which rage decreases while player is not visible
    public float originalRageDecreaseMultiplier = 1f; // Default value for rage decrease multiplier

    // -------- Nudge Cooldown (to prevent constant updates) --------
    private float nudgeCooldownTimer = 0f;

    // -------- Roaming & Searching --------
    [Header("Roam settings")]
    private Vector3 roamTarget;
    private List<Vector3> visitedLocations = new List<Vector3>();
    private List<Vector3> navMeshPoints = new List<Vector3>(); // Precomputed NavMesh points
    private float roamTimer = 0f;
    public float roamInterval = 30f; // Interval between new roam targets

    // -------- Investigating --------
    private Vector3 investigateTarget;
    private bool hasInvestigateTarget = false;

    // -------- Searching --------
    [Header("Search settings")]
    private Vector3 lastSeenPlayerPos;
    private bool isSearching = false;
    public float searchTime = 15f; // Updated search time to 15 seconds
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

        // Set rage-modified values (base value + increase).
        rageSight = sightRange + rageSightIncrease;
        rageHearing = hearingRange + rageHearingIncrease;
        rageWalk = walkSpeed + rageWalkIncrease;
        rageRun = runSpeed + rageRunIncrease;
        rageDecreaseMultiplier = originalRageDecreaseMultiplier; // Initialize the multiplier.

        // Precompute NavMesh points for efficient roaming.
        PrecomputeNavMeshPoints();
    }

    void Update()
    {
        // Update nudge cooldown timer.
        nudgeCooldownTimer = Mathf.Max(nudgeCooldownTimer - Time.deltaTime, 0f);

        // Update roaming target periodically if not enraged.
        roamTimer += Time.deltaTime;
        if (roamTimer >= roamInterval && !isEnraged)
        {
            roamTimer = 0f;
            SetNewRoamDestination();
        }

        // Execute behavior based on current state.
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

        // Constantly check for the player and update rage.
        CheckForPlayer();
        HandleRage();
        ClampSpeed();
    }


    // ======================================================
    // =================== UTILITY METHODS ==================
    // ======================================================

    /// <summary>
    /// Clamps the agent's speed based on current state and enraged status.
    /// </summary>
    void ClampSpeed()
    {
        if (currentState == EnemyState.Roaming || currentState == EnemyState.Searching || currentState == EnemyState.Investigating)
        {
            agent.speed = isEnraged ? rageWalk : originalWalkSpeed;
        }
        else if (currentState == EnemyState.Chasing || currentState == EnemyState.Enraged)
        {
            agent.speed = isEnraged ? rageRun : originalRunSpeed;
        }
    }

    /// <summary>
    /// Precomputes NavMesh points for use in random roaming.
    /// </summary>
    void PrecomputeNavMeshPoints()
    {
        NavMeshTriangulation navMeshData = NavMesh.CalculateTriangulation();
        foreach (Vector3 vertex in navMeshData.vertices)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(vertex, out hit, 1f, NavMesh.AllAreas))
            {
                navMeshPoints.Add(hit.position);
            }
        }
    }

    /// <summary>
    /// Returns a random NavMesh location that hasn’t been visited recently.
    /// </summary>
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

    /// <summary>
    /// Checks if the player is visible and updates the last seen position.
    /// Transitions to Chasing if the player is in view (when not enraged).
    /// </summary>
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

    /// <summary>
    /// Returns true if the player is currently visible.
    /// </summary>
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

    /// <summary>
    /// Roaming behavior: move toward a random destination.
    /// </summary>
    void Roam()
    {
        agent.speed = walkSpeed;
        if (Vector3.Distance(transform.position, roamTarget) < 2f)
            SetNewRoamDestination();
    }

    void SetNewRoamDestination()
    {
        roamTarget = GetRandomNavMeshLocation();
        agent.SetDestination(roamTarget);
        if (Vector3.Distance(transform.position, roamTarget) > 2f)
        {
            visitedLocations.Add(roamTarget);
            if (visitedLocations.Count > 10)
                visitedLocations.RemoveAt(0);
        }
    }

    void ChasePlayer()
    {
        agent.speed = runSpeed;
        if (player != null)
            agent.SetDestination(player.position);

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
            searchTimer = 0f;
            currentState = EnemyState.Searching;
            SetNewSearchDestination();
        }
    }

    void SetNewSearchDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * 15f;
        randomDirection += lastSeenPlayerPos;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void SearchArea()
    {
        if (player == null)
            return;

        if (agent.pathPending == false && agent.remainingDistance <= 2f)
        {
            // Do nothing here; just wait for the timer to finish.
        }

        searchTimer += Time.deltaTime;
        if (searchTimer >= searchTime)
        {
            currentState = EnemyState.Roaming;
            isSearching = false;
            searchTimer = 0f;
        }
    }

    // ======================================================
    // ===================== RAGE SYSTEM ====================
    // ======================================================

    /// <summary>
    /// Updates the rage meter:
    /// - Increases rage when the player is visible.
    /// - After 4 seconds of not seeing the player, rage decreases.
    /// - If not in cooldown and rage reaches max, enter enraged state.
    /// - If previously enraged and rage drops below 70% of max, trigger a 10‑second cooldown.
    /// </summary>
    void HandleRage()
    {
        if (currentRage == maxRage)
        {
            currentState = EnemyState.Enraged;
        }
        else
        {
            currentState = EnemyState.Normal;
        }
        if (CanSeePlayer())
        {
            rageDecreaseMultiplier = originalRageDecreaseMultiplier; // Reset the multiplier when the player is visible.
            outOfSightTimer = 0f;
            currentRage += Time.deltaTime * rageIncreaseRate;
        }
        else
        {
            outOfSightTimer += Time.deltaTime;
            if (outOfSightTimer >= 4f)
            {
                rageDecreaseMultiplier += rageDecreaseRate * Time.deltaTime; 
                currentRage -= Time.deltaTime * rageDecreaseRate * rageDecreaseMultiplier;
            }
            
        }
        currentRage = Mathf.Clamp(currentRage, 0, maxRage);

        if (!inRageCooldown && currentRage >= maxRage)
        {
            currentRage = maxRage;
            isEnraged = true;
            currentState = EnemyState.Enraged;
        }
        else if (isEnraged && currentRage < 0.5f * maxRage && !inRageCooldown)
        {
            isEnraged = false;
            inRageCooldown = true;
            rageCooldownTimer = 10f;
            currentState = EnemyState.Chasing;
        }

        if (inRageCooldown)
        {
            rageCooldownTimer -= Time.deltaTime;
            if (rageCooldownTimer <= 0f)
            {
                inRageCooldown = false;
                if (currentRage < 0.5f * maxRage)
                {
                    currentState = EnemyState.Chasing;
                }
            }
        }
    }


    /// <summary>
    /// Behavior while enraged:
    /// - If the player is visible, chase directly.
    /// - Otherwise, if currentRage is high (≥75% of max), nudge toward a point 45 meters from the player.
    /// - If close to its target (e.g. last seen position), force a nudge to avoid freezing.
    /// </summary>
    void EnragedBehavior()
{
    if( currentRage < 0.7f * maxRage)
    {
        runSpeed = rageRun;
        walkSpeed = rageWalk;
        sightRange = rageSight;
        hearingRange = rageHearing;
    }
    else
    {
        runSpeed = originalRunSpeed;
        walkSpeed = originalWalkSpeed;
        sightRange = originalSightRange;
        hearingRange = originalHearingRange;
    }


    if (player != null)
    {
        if (CanSeePlayer())
        {
            // Only chase the player if the AI can see them and is enraged.
            agent.SetDestination(player.position);
        }
        else
        {
            // Nudge if enraged and can’t see the player.
            if (agent.remainingDistance <= 2f || currentRage >= 0.75f * maxRage)
            {
                if (nudgeCooldownTimer <= 0f)
                    NudgeTowardsPlayer();
            }
            else
            {
                agent.SetDestination(lastSeenPlayerPos);
            }
        }
    }
}

    void NormalBehavior()  // Add this behavior for when rage is low.
{
    runSpeed = originalRunSpeed;
    walkSpeed = originalWalkSpeed;
    sightRange = originalSightRange;
    hearingRange = originalHearingRange;

    if (player != null)
    {
        if (CanSeePlayer()) // Only move towards player if it's within sight.
        {
            agent.SetDestination(player.position);
        }
        else
        {
            // Move towards the last known position of the player.
            agent.SetDestination(lastSeenPlayerPos);
        }
    }
}


    /// <summary>
    /// Nudges the enemy toward a target point exactly 45 meters from the player's current position.
    /// The target is validated on the NavMesh to avoid off-map navigation.
    /// A nudge cooldown prevents constant updating.
    /// </summary>
    void NudgeTowardsPlayer()
    {
        if (player == null)
            return;

        if (nudgeCooldownTimer > 0f)
            return; // Don't update too frequently.

        Vector3 direction = (transform.position - player.position).normalized;
        Vector3 targetPosition = player.position + direction * 45f;

        // Find a valid NavMesh position.
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 1f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            nudgeCooldownTimer = 3f; // Set a cooldown time (increase this to prevent constant nudging)
        }
        else
        {
            // If no valid position is found, nudge towards the last known position as a fallback.
            agent.SetDestination(lastSeenPlayerPos);
            nudgeCooldownTimer = 3f; // Set a cooldown time (increase this to prevent constant nudging)
        }
    }

}
