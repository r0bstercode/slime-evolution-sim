using UnityEngine;
using UnityEngine.InputSystem;

public enum ToolMode
{
    None,
    FoodPaint,
    Obstacle,
    Erase,
    Spawn
}

[System.Serializable]
public struct Corpse
{
    public bool active;
    public Vector2 position;
    public float energy;
    public float age;
    public int speciesIndex;
}

[System.Serializable]
public struct Nest
{
    public bool active;
    public Vector2 position;
    public int speciesIndex;
    public float storedEnergy;
}

public partial class SimulationManager : MonoBehaviour
{
    [Header("Species")]
    public SpeciesDNA[] species;

    public const int MaxSpecies = 32;
    public int activeSpeciesCount = 3;
    private int simulationSenseTick = 0;

    [Header("Performance")]
    public bool showPerformanceStats = true;
    private float cachedFps;
    private float cachedFrameMs;
    private float cachedSimMs;
    private float perfRefreshTimer = 0f;
    public float perfRefreshInterval = 0.25f;
    public int trailUpdateEveryNFrames = 5;
    public int foodUpdateEveryNFrames = 5;
    public int agentUpdateStride = 2;
    private int agentUpdateOffset = 0;
    private float cachedPreyGridMs;
    private float cachedAgentsMs;
    private float cachedCorpsesMs;
    private float cachedTrailsMs;
    private float cachedFoodMs;
    private int trailSpeciesUpdateIndex = 0;
    public int foodUpdateStripes = 4;
    private int foodStripeIndex = 0;
    public bool showTrails = true;

    [Header("Navigation Trails")]
    public float homeTrailDecayRate = 0.0002f;
    public float foodTrailDecayRate = 0.5f;
    public float navTrailDiffusionRate = 0.1f;
    private int navTrailUpdateTick = 0;
    public int navTrailUpdateEveryNSteps = 8;


    [Header("Evolution")]
    public bool mutationEnabled = false;
    public float mutationStrength = 0.05f;
    public float speciationThreshold = 1.5f;
    private int nextFreeAgentSearchIndex = 0;

    [Header("Evolution Stats")]
    public int mutationEventCount = 0;
    public int speciationEventCount = 0;
    public int extinctionEventCount = 0;

    public bool pauseOnSpeciation = false;
    public bool speciationEnabled = false;
    

    [Header("Simulation")]
    public int startingAgentCount = 10000;
    public Vector2 simulationArea = new Vector2(100, 100);
    public bool simulationPaused = true;
    public float worldAge = 0f;
    public float simulationSpeed = 1f;
    public bool panicMode = false;

    [Header("Nests")]
    public int maxNests = 32;
    public float nestRadius = 2.5f;
    public float nestReturnEnergyThreshold = 100f;
    public float nestRestTime = 2f;

    private Nest[] nests;

    [Header("Nest Spawning")]
    public bool staggerInitialSpawn = true;
    public float nestSpawnInterval = 100f;
    public int nestSpawnBatchSize = 1;

    private int[] targetSpeciesPopulation;
    private int[] spawnedSpeciesPopulation;
    private float nestSpawnTimer = 0f;

    public int initialSpawnEveryNSimTicks = 30;
    private int initialSpawnTick = 1;

    [Header("Nest Reproduction")]
    public float nestSpawnEnergyCost = 120f;
    public float nestReproductionInterval = 1f;
    public int nestMaxAgentsPerSpecies = 2000;

    private float nestReproductionTimer = 0f;

    [Header("Simulation Speed")]
    public float fixedSimulationDt = 0.02f;
    

    private int GetSimulationStepsPerFrame()
    {
        if (simulationSpeed <= 1f)
        {
            agentUpdateStride = 1;
            return 1;
        }

        if (simulationSpeed <= 5f)
        {
            agentUpdateStride = 2;
            return 5;
        }

        if (simulationSpeed <= 10f)
        {
            agentUpdateStride = 2;
            return 10;
        }

        if (simulationSpeed <= 25f)
        {
            agentUpdateStride = 3;
            return 25;
        }

        agentUpdateStride = 4;
        return 50;
    }

    [Header("Random Obstacles")]
    public bool randomObstaclesEnabled = true;
    public int randomObstacleBlobCount = 12;
    public int randomObstacleBlobRadius = 8;

    [Header("Population")]
    public int startingPopulationPerSpecies = 1000;

    [Header("Cached Stats")]
    public float statsRefreshInterval = 0.25f;

    private int cachedTotalAlive;
    private int[] cachedSpeciesCounts;
    private float cachedTotalFood;
    private float cachedFoodCoverage;
    private int cachedCorpseCount;

    private float cachedAgentEnergy;
    private float cachedGreenEnergy;
    private float cachedBrownEnergy;
    private float cachedCorpseEnergy;
    private float cachedEcoEnergyTotal;

    private int[] cachedTopEnergySpecies = new int[3];
    private float[] cachedTopEnergyAmounts = new float[3];

    private float statsRefreshTimer = 0f;

    [Header("Species Event Stats")]
    private int[] cachedSpeciesBirths;
    private int[] cachedSpeciesDeaths;
    private int[] totalSpeciesBirths;
    private int[] totalSpeciesDeaths;
    private int[] previousSpeciesBirths;
    private int[] previousSpeciesDeaths;
    private float[] cachedSpeciesBirthRate;
    private float[] cachedSpeciesDeathRate;



    [Header("Population Graph")]
    public bool showPopulationGraph = true;
    public int populationGraphSamples = 120;
    public float populationGraphInterval = 0.5f;
    public float populationGraphHeight = 90f;

    private float populationGraphTimer = 0f;
    private int populationGraphIndex = 0;
    private int[,] populationHistory;

    
    [Header("Corpses")]
    public int maxCorpses = 5000;
    public float corpseDecayRate = 0.000000003f;
    private float[,] corpseGrid;

    private Corpse[] corpses;

    [Header("Corpse Feeding")]
    public float corpseEatAmount = 50f;

    [Header("Mutation Budget")]
    public float mutationCooldownPerSpecies = 5f;

    private float[] mutationCooldowns;

    [Header("Speciation Budget")]
    public float speciesSpeciationCooldown = 60f;
    public float globalSpeciationCooldown = 10f;
    public float founderEnergyMultiplier = 5f;
    public int founderExtraCount = 3;

    private float[] speciesSpeciationCooldowns;
    private float globalSpeciationTimer = 0f;


    [Header("Predation")]
    public float preyScavengerThreshold = 1.5f;

    [Header("Predator Attack")]
    public float preyAttackRange = 0.6f;
    public float preyAttackEnergyCost = 0.2f;
    public float preyAttackDamage = 5f;
    public float preyAttackSlowDuration = 2f;
    public float preyAttackSlowMultiplier = 0.25f;

    [Header("Trail Grid")]
    public int gridWidth = 200;
    public int gridHeight = 200;
    public float trailDecayRate = 2f;
    public float trailDiffusionRate = 4f;
    private float[,,] homeDistanceGrid;
    private float[,,] nextHomeDistanceGrid;


    [Header("Food Grid")]
    public float initialFoodAmount = 0.45f;
    public float foodGrowthRate = 0.02f;
    public float foodEatAmount = 100f;
    private float[,,] preyGrid;
    private float[,] totalPreyGrid;

    [Header("Energy Economy")]
    public float sunlightEnergyPerSecond = 5000f;
    public float maxEcoEnergy = 600000f;

    [Header("Food Types")]
    public FoodType[] foodTypes;
    public int selectedFoodType = 0;
    public float brownToGreenRate = 0.02f;

    [Header("Danger System")]
    public float dangerDeposit = 2f;
    public float dangerDecay = 1f;
    public float dangerDiffusion = 0.5f;
    public float preyDangerAvoidance = 3f;
    public float predatorThreshold = 0.5f;

    [Header("Obstacles")]
    public float obstacleChance = 0.01f;

    private SlimeAgent[] agents;
    private RuntimeSpecies[] runtimeSpecies;
    private float[,,] trailGrid;
    private float[,,] nextTrailGrid;
    private float[,,] foodGrid;
    private bool[,] obstacleGrid;
    private int[,] agentIndexGrid;
    private float[,] dangerGrid;
    private float[,] nextDangerGrid;

    private float[,,] homeTrailGrid;
    private float[,,] nextHomeTrailGrid;

    private float[,,] foodTrailGrid;
    private float[,,] nextFoodTrailGrid;


    public float agentDrawSize = 0.06f;
    public float foodAlpha = 0.18f;
    public float trailAlpha = 1.2f;
    public float trailMinimumVisible = 0.02f;

    [Header("Mouse Tools")]
    public ToolMode currentTool = ToolMode.None;
    public float brushRadius = 3f;
    public float brushStrength = 500f;
    public bool mouseToolsEnabled = true;
    public float brushScrollSpeed = 1f;

    public int selectedSpeciesIndex = 0;
    public int spawnCountPerClick = 50;
    public float spawnRadius = 2f;

    private void Start()
    {
        runtimeSpecies = new RuntimeSpecies[MaxSpecies];
        mutationCooldowns = new float[MaxSpecies];
        speciesSpeciationCooldowns = new float[MaxSpecies];
        cachedSpeciesBirths = new int[MaxSpecies];
        cachedSpeciesDeaths = new int[MaxSpecies];
        totalSpeciesBirths = new int[MaxSpecies];
        totalSpeciesDeaths = new int[MaxSpecies];
        previousSpeciesBirths = new int[MaxSpecies];
        previousSpeciesDeaths = new int[MaxSpecies];
        cachedSpeciesBirthRate = new float[MaxSpecies];
        cachedSpeciesDeathRate = new float[MaxSpecies];

        for (int i = 0; i < MaxSpecies; i++)
        {
            runtimeSpecies[i] = new RuntimeSpecies();
        }

        activeSpeciesCount = Mathf.Min(activeSpeciesCount, species.Length, MaxSpecies);

        for (int i = 0; i < activeSpeciesCount; i++)
        {
            runtimeSpecies[i].CopyFromDNA(species[i]);
        }

        trailGrid = new float[gridWidth, gridHeight, MaxSpecies];
        nextTrailGrid = new float[gridWidth, gridHeight, MaxSpecies];

        homeTrailGrid = new float[gridWidth, gridHeight, MaxSpecies];
        nextHomeTrailGrid = new float[gridWidth, gridHeight, MaxSpecies];

        homeDistanceGrid = new float[gridWidth, gridHeight, MaxSpecies];
        nextHomeDistanceGrid = new float[gridWidth, gridHeight, MaxSpecies];

        foodTrailGrid = new float[gridWidth, gridHeight, MaxSpecies];
        nextFoodTrailGrid = new float[gridWidth, gridHeight, MaxSpecies];

        obstacleGrid = new bool[gridWidth, gridHeight];
        InitializeObstacles();
        obstaclesDirty = true;

        InitializeFoodTypes();

        foodGrid = new float[gridWidth, gridHeight, foodTypes.Length];
        dangerGrid = new float[gridWidth, gridHeight];
        nextDangerGrid = new float[gridWidth, gridHeight];
        InitializeFood();

        InitializeNests();
        InitializeAgents();
        AssignAgentsToNests();

        corpses = new Corpse[maxCorpses];
        corpseGrid = new float[gridWidth, gridHeight];

        preyGrid = new float[gridWidth, gridHeight, MaxSpecies];
        totalPreyGrid = new float[gridWidth, gridHeight];
        agentIndexGrid = new int[gridWidth, gridHeight];

        
        InitializeFoodRenderer();
        InitializeTrailRenderer();
        InitializeObstacleRenderer();
        InitializeAgentTextureRenderer();

        simulationPaused = true;
        worldAge = 0f;
        Debug.Log("Terrarium initialized.");
        Debug.Log("Active species: " + activeSpeciesCount);
        Debug.Log("Spawned agents: " + agents.Length);
    }

    private void Update()
    {
        Time.timeScale = 1f;

        HandleHotkeys();
        HandleBrushSize();
        HandleMouseTools();

        if (!simulationPaused)
        {
            float simStart = Time.realtimeSinceStartup;

            int steps = GetSimulationStepsPerFrame();

            float preyGridMs = 0f;
            float agentsMs = 0f;
            float corpsesMs = 0f;
            float trailsMs = 0f;
            float foodMs = 0f;

            for (int step = 0; step < steps; step++)
            {
                float dt = fixedSimulationDt;

                worldAge += dt;

                float t0 = Time.realtimeSinceStartup;

                if (step == 0)
                    RebuildPreyGrid();

                float t1 = Time.realtimeSinceStartup;

                SimulateAgents();

                float t2 = Time.realtimeSinceStartup;

                UpdateCorpses(dt);

                float t3 = Time.realtimeSinceStartup;

                if ((Time.frameCount + step) % trailUpdateEveryNFrames == 0)
                {
                    DiffuseAndDecayTrails(dt);
                    DiffuseNavigationTrails(dt);
                    DiffuseAndDecayDanger(dt);
                }

                float t4 = Time.realtimeSinceStartup;

                if ((Time.frameCount + step) % foodUpdateEveryNFrames == 0)
                {
                    RegrowFood(dt);
                }

                float t5 = Time.realtimeSinceStartup;

                preyGridMs += (t1 - t0) * 1000f;
                agentsMs += (t2 - t1) * 1000f;
                corpsesMs += (t3 - t2) * 1000f;
                trailsMs += (t4 - t3) * 1000f;
                foodMs += (t5 - t4) * 1000f;

                if (globalSpeciationTimer > 0f)
                    globalSpeciationTimer -= dt;

                for (int i = 0; i < activeSpeciesCount; i++)
                {
                    if (speciesSpeciationCooldowns[i] > 0f)
                        speciesSpeciationCooldowns[i] -= dt;
                }

                statsRefreshTimer += dt;

                if (statsRefreshTimer >= statsRefreshInterval)
                {
                    RefreshCachedStats();

                    float interval = Mathf.Max(0.0001f, statsRefreshTimer);

                    for (int i = 0; i < activeSpeciesCount; i++)
                    {
                        int birthsDelta =
                            totalSpeciesBirths[i] - previousSpeciesBirths[i];

                        int deathsDelta =
                            totalSpeciesDeaths[i] - previousSpeciesDeaths[i];

                        cachedSpeciesBirthRate[i] = birthsDelta / interval;
                        cachedSpeciesDeathRate[i] = deathsDelta / interval;

                        previousSpeciesBirths[i] = totalSpeciesBirths[i];
                        previousSpeciesDeaths[i] = totalSpeciesDeaths[i];
                    }

                    statsRefreshTimer = 0f;
                }

                populationGraphTimer += dt;

                if (populationGraphTimer >= populationGraphInterval)
                {
                    RecordPopulationHistory();
                    populationGraphTimer = 0f;
                }
            }

            UpdateFoodTexture();
            UpdateTrailTexture();
            DiffuseNavigationTrails(fixedSimulationDt);
            UpdateObstacleTexture();
            UpdateAgentTexture();
            UpdateNestReproduction(Time.deltaTime);
            UpdateInitialNestSpawning();
            DepositNestHomeTrails(fixedSimulationDt);

            perfRefreshTimer += Time.unscaledDeltaTime;

            if (perfRefreshTimer >= perfRefreshInterval)
            {
                cachedSimMs = (Time.realtimeSinceStartup - simStart) * 1000f;
                cachedFrameMs = Time.unscaledDeltaTime * 1000f;
                cachedFps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);

                cachedPreyGridMs = preyGridMs;
                cachedAgentsMs = agentsMs;
                cachedCorpsesMs = corpsesMs;
                cachedTrailsMs = trailsMs;
                cachedFoodMs = foodMs;

                perfRefreshTimer = 0f;
            }
        }
        
    }

    private void UpdateInitialNestSpawning()
    {
        if (!staggerInitialSpawn)
            return;

        if (targetSpeciesPopulation == null ||
            spawnedSpeciesPopulation == null)
            return;

        initialSpawnTick++;

        if (initialSpawnTick % initialSpawnEveryNSimTicks != 0)
            return;

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            if (spawnedSpeciesPopulation[s] >= targetSpeciesPopulation[s])
                continue;

            for (int b = 0; b < nestSpawnBatchSize; b++)
            {
                if (spawnedSpeciesPopulation[s] >= targetSpeciesPopulation[s])
                    break;

                if (SpawnAgentFromNest(s))
                    spawnedSpeciesPopulation[s]++;
            }
        }
    }

}