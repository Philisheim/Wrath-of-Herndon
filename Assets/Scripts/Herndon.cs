using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace WrathOfHerndon
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class Herndon : MonoBehaviour
    {
        // ======================================================
        // ================ ENUM & VARIABLES ==================
        // ======================================================
        public enum EnemyState { Roaming, Chasing, Investigating, Searching, Enraged }
        private EnemyState currentState;
        private EnemyState lastState; // Tracks the previous state

        private NavMeshAgent agent;
        private Transform player;
        private Screen_Shake ss;
        private Animator animator; // Animator component reference

        // To track which animation is currently playing
        private string currentAnimState = "";

        // -------- Movement Settings --------
        [Header("Movement Speeds")]
        public float walkSpeed = 3.5f;
        public float runSpeed = 6.0f;

        // -------- Detection Settings --------
        [Header("Detection Settings")]
        public float sightRange = 15f;
        public float hearingRange = 25f;
        public float hearingRadius = 5f; // Radius for investigating a noise

        // Rage-modified values (base + increase)
        private float rageSight;
        private float rageHearing;
        private float rageWalk;
        private float rageRun;
        public float rageSightIncrease;
        public float rageHearingIncrease;
        public float rageWalkIncrease;
        public float rageRunIncrease;
        public LayerMask playerLayer;
        public LayerMask obstacleLayer;

        // -------- Rage Settings --------
        [Header("Rage Buff Settings")]
        public float maxRage = 100f;
        public float rageIncreaseRate = 10f;
        public float rageDecreaseRate = 5f;
        public float currentRage = 0f;
        private bool isEnraged = false;
        private float rageCooldownTimer = 0f;
        private bool inRageCooldown = false;
        private float outOfSightTimer = 0f;
        [Tooltip("Percentage of max rage before rage buffs are lost.")]
        public float rageThreshold = 0.55f;

        [Header("Rage Decrease Settings")]
        [Tooltip("Time before rage starts decreasing when out of sight.")]
        public float maxOutOfSight = 4f;
        [Tooltip("How fast the rage decrease multiplier increases per second.")]
        public float rageDecreaseMultiplierIncrease = 0.5f;
        private float currentRageDecreaseMultiplier = 1f;

        // -------- Nudge Settings --------
        [Header("Nudge Settings")]
        [Tooltip("Radius around the player from which a nudge target is chosen.")]
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
            ss = Camera.main.GetComponent<Screen_Shake>();
            animator = GetComponent<Animator>(); // Get the Animator component

            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            currentState = EnemyState.Roaming;
            lastState = currentState; // Initialize lastState
            SetNewRoamDestination();

            // Cache original values
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

            // Set up the SphereCollider as a trigger for hearing.
            SphereCollider sc = GetComponent<SphereCollider>();
            sc.radius = hearingRange;
            sc.isTrigger = true;
        }

        void Update()
        {
            // When the state changes, reset roamTimer (unless in Searching state)
            if (currentState != lastState)
            {
                if (currentState != EnemyState.Searching)
                {
                    roamTimer = 0;
                }
                lastState = currentState;
            }

            // Increment roamTimer if roaming
            if (currentState == EnemyState.Roaming)
            {
                roamTimer += Time.deltaTime;
                if (roamTimer >= roamInterval && !isEnraged)
                {
                    roamTimer = 0f;
                    SetNewRoamDestination();
                }
            }

            if (currentState == EnemyState.Roaming && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                roamTimer = 0f;
            }

            nudgeCooldownTimer = Mathf.Max(nudgeCooldownTimer - Time.deltaTime, 0f);

            // State-based logic
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

            // Update the animator based on the current state and movement.
            UpdateAnimationState();
        }

        // ======================================================
        // =================== UTILITY METHODS ==================
        // ======================================================
        void ClampSpeed()
        {
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
            roamTimer = Mathf.Min(roamTimer + Time.deltaTime, roamInterval);
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

        public void HearNoise(Vector3 noisePosition, GameObject noiseSource = null)
        {
            if (CanSeePlayer())
                return;

            Vector3 randomOffset = Random.insideUnitSphere * hearingRadius;
            randomOffset.y = 0;
            investigateTarget = noisePosition + randomOffset;
            hasInvestigateTarget = true;
            currentState = EnemyState.Investigating;

            if (noiseSource != null)
            {
                noiseSource.tag = "Heard";
            }
        }

        void Investigate()
        {
            agent.speed = walkSpeed;
            if (hasInvestigateTarget)
            {
                agent.SetDestination(investigateTarget);
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    hasInvestigateTarget = false;
                    if (!CanSeePlayer())
                    {
                        currentState = EnemyState.Roaming;
                        SetNewRoamDestination();
                    }
                    else
                    {
                        currentState = EnemyState.Chasing;
                    }
                }
            }
        }

        Vector3 GetFallbackDestination(Vector3 lastKnownPos)
        {
            Vector3 dirToLastSeen = (lastKnownPos - transform.position).normalized;
            int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * searchRadius;
                randomOffset.y = 0;
                Vector3 potentialTarget = lastKnownPos + randomOffset;
                Vector3 dirToPotential = (potentialTarget - transform.position).normalized;
                if (Vector3.Dot(dirToPotential, dirToLastSeen) > 0.5f)
                {
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(potentialTarget, out hit, 5f, NavMesh.AllAreas))
                    {
                        return hit.position;
                    }
                }
            }
            Vector3 fallbackTarget = transform.position + dirToLastSeen * searchRadius;
            NavMeshHit fallbackHit;
            if (NavMesh.SamplePosition(fallbackTarget, out fallbackHit, 5f, NavMesh.AllAreas))
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

            float threshold = agent.stoppingDistance + 0.5f;
            if (!agent.pathPending && agent.remainingDistance <= threshold)
            {
                if (!CanSeePlayer())
                {
                    currentState = EnemyState.Roaming;
                    isSearching = false;
                    SetNewRoamDestination();
                }
                else
                {
                    currentState = EnemyState.Chasing;
                }
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
                currentRageDecreaseMultiplier = 1f;
                currentRage += Time.deltaTime * rageIncreaseRate;
            }
            else
            {
                outOfSightTimer += Time.deltaTime;
                if (outOfSightTimer >= maxOutOfSight)
                {
                    currentRageDecreaseMultiplier += rageDecreaseMultiplierIncrease * Time.deltaTime;
                    currentRage -= Time.deltaTime * rageDecreaseRate * currentRageDecreaseMultiplier;
                }
            }
            currentRage = Mathf.Clamp(currentRage, 0, maxRage);

            if (!inRageCooldown)
            {
                if (!isEnraged && currentRage >= maxRage)
                {
                    if (ss != null)
                    {
                        ss.shake = true;
                        ss.Shake();
                    }
                    currentRage = maxRage;
                    isEnraged = true;
                    currentState = EnemyState.Enraged;
                }
                else if (isEnraged && currentRage < rageThreshold * maxRage)
                {
                    isEnraged = false;
                    inRageCooldown = true;
                    rageCooldownTimer = 2f;
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
                        agent.SetDestination(player.position);
                    }
                    else
                    {
                        if (nudgeCooldownTimer <= 0f)
                        {
                            NudgeTowardsPlayer();
                            nudgeCooldownTimer = 2f;
                        }
                    }
                }
            }
            else
            {
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

        void NudgeTowardsPlayer()
        {
            if (player == null)
                return;

            float currentDistance = Vector3.Distance(transform.position, player.position);
            Vector3 targetPosition = player.position; // Fallback
            bool foundValidTarget = false;
            int maxAttempts = 10;
            for (int i = 0; i < maxAttempts; i++)
            {
                Vector2 randomPoint = Random.insideUnitCircle * nudgeRadius;
                Vector3 potentialTarget = player.position + new Vector3(randomPoint.x, 0, randomPoint.y);

                if (Vector3.Distance(player.position, potentialTarget) < currentDistance)
                {
                    targetPosition = potentialTarget;
                    foundValidTarget = true;
                    break;
                }
            }

            if (!foundValidTarget)
            {
                targetPosition = player.position;
            }

            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                lastNudgeTarget = hit.position;
            }
            else
            {
                agent.SetDestination(player.position);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                SceneManager.LoadScene(1);
            }
        }

        // ======================================================
        // =================== HEARING SYSTEM ===================
        // ======================================================
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Noisy"))
            {
                HearNoise(other.transform.position, other.gameObject);
            }
        }

        // ======================================================
        // ============== ANIMATION MANAGEMENT ==================
        // ======================================================
        // This method updates the animations based on the enemy’s current state and behavior.
        // It directly plays the named animation states ("Idle", "Walk", "Run") defined in your Animator Controller.
        void UpdateAnimationState()
        {
            string newAnimState = "";

            // Use state and movement to decide which animation to play.
            switch (currentState)
            {
                // For states where the enemy is not actively chasing
                case EnemyState.Roaming:
                case EnemyState.Investigating:
                case EnemyState.Searching:
                    // If the enemy is nearly stationary then Idle, otherwise Walk.
                    if (agent.velocity.magnitude < 0.1f)
                        newAnimState = "Idle";
                    else
                        newAnimState = "Walk";
                    break;

                // For aggressive states
                case EnemyState.Chasing:
                case EnemyState.Enraged:
                    newAnimState = "Run";
                    break;
            }

            // Only change the animation if it differs from the current one.
            if (animator != null)
            {
                if (newAnimState != currentAnimState)
                {
                    animator.Play(newAnimState);
                    currentAnimState = newAnimState;
                }
            }
        }
    }
}
