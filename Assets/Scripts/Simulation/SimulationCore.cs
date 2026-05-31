using UnityEngine;

public partial class SimulationManager
{
    private void InitializeNests()
    {
        nests = new Nest[maxNests];

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            if (s >= maxNests)
                break;

            nests[s] = new Nest
            {
                active = true,
                position = GetRandomNestPosition(s),
                speciesIndex = s,
                storedEnergy = 0f
            };
        }
    }

    private void InitializeAgents()
    {
        agents = new SlimeAgent[startingAgentCount];

        targetSpeciesPopulation = new int[MaxSpecies];
        spawnedSpeciesPopulation = new int[MaxSpecies];

        for (int speciesIndex = 0; speciesIndex < activeSpeciesCount; speciesIndex++)
        {
            RuntimeSpecies dna = runtimeSpecies[speciesIndex];

            int startCount = startingPopulationPerSpecies;

            if (dna.sourceDNA != null)
                startCount = dna.sourceDNA.startingPopulation;

            targetSpeciesPopulation[speciesIndex] = startCount;
            spawnedSpeciesPopulation[speciesIndex] = 0;
        }

        if (!staggerInitialSpawn)
        {
            for (int speciesIndex = 0; speciesIndex < activeSpeciesCount; speciesIndex++)
            {
                for (int i = 0; i < targetSpeciesPopulation[speciesIndex]; i++)
                {
                    SpawnAgentFromNest(speciesIndex);
                }
            }
        }
    }

    private bool SpawnAgentFromNest(int speciesIndex)
    {
        int slot = FindFreeAgentSlot();

        if (slot < 0)
            return false;

        RuntimeSpecies dna = runtimeSpecies[speciesIndex];

        int nestIndex = FindNestForSpecies(speciesIndex);

        Vector2 spawnPosition = GetRandomFreePosition();

        if (nestIndex >= 0)
        {
            spawnPosition =
                nests[nestIndex].position +
                Random.insideUnitCircle * nestRadius;
        }

        if (IsObstacleAt(spawnPosition))
            spawnPosition = GetRandomFreePosition();

        agents[slot] = new SlimeAgent
        {
            position = spawnPosition,
            angle = Random.Range(0f, 360f),
            speciesIndex = speciesIndex,
            age = 0f,
            energy = dna.startEnergy,
            alive = true,
            hp = 1f,
            slowTimer = 0f,
            slowMultiplier = 1f,
            pauseTimer = 0f,
            lockedCorpseIndex = -1,
            lockedFoodX = -1,
            lockedFoodY = -1,
            lockedFoodType = -1,
            foodTrailTimer = 0f,
            foodTrailStrength = 0f,

            mode = AgentMode.Foraging,
            homeNestIndex = nestIndex,

            cachedLeftSense = 0f,
            cachedForwardSense = 0f,
            cachedRightSense = 0f,
            senseCacheValid = false
        };

        return true;
    }

    private void AssignAgentsToNests()
    {
        if (nests == null || agents == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            int speciesIndex = agents[i].speciesIndex;

            for (int n = 0; n < nests.Length; n++)
            {
                if (!nests[n].active)
                    continue;

                if (nests[n].speciesIndex != speciesIndex)
                    continue;

                agents[i].homeNestIndex = n;
                break;
            }
        }
    }

    private int FindNestForSpecies(int speciesIndex)
    {
        if (nests == null)
            return -1;

        for (int i = 0; i < nests.Length; i++)
        {
            if (!nests[i].active)
                continue;

            if (nests[i].speciesIndex == speciesIndex)
                return i;
        }

        return -1;
    }

    private Vector2 GetRandomFreePosition()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector2 position = new Vector2(
                Random.Range(-simulationArea.x * 0.5f, simulationArea.x * 0.5f),
                Random.Range(-simulationArea.y * 0.5f, simulationArea.y * 0.5f)
            );

            if (!IsObstacleAt(position))
                return position;
        }

        return Vector2.zero;
    }

    private Vector2 GetRandomNestPosition(int speciesIndex)
    {
        float margin = 12f;
        float minNestDistance = 25f;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector2 position = new Vector2(
                Random.Range(-simulationArea.x * 0.5f + margin, simulationArea.x * 0.5f - margin),
                Random.Range(-simulationArea.y * 0.5f + margin, simulationArea.y * 0.5f - margin)
            );

            if (IsObstacleAt(position))
                continue;

            bool tooClose = false;

            for (int i = 0; i < nests.Length; i++)
            {
                if (!nests[i].active)
                    continue;

                if (Vector2.Distance(position, nests[i].position) < minNestDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                return position;
        }

        return GetRandomFreePosition();
    }

    private void ResetSimulation()
    {
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

        foodGrid = new float[gridWidth, gridHeight, foodTypes.Length];
        
        dangerGrid = new float[gridWidth, gridHeight];
        nextDangerGrid = new float[gridWidth, gridHeight];

        InitializeNests();
        InitializeFood();
        InitializeAgents();
        AssignAgentsToNests();
        corpses = new Corpse[maxCorpses];
        corpseGrid = new float[gridWidth, gridHeight];
        preyGrid = new float[gridWidth, gridHeight, MaxSpecies];
        totalPreyGrid = new float[gridWidth, gridHeight];
        agentIndexGrid = new int[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                agentIndexGrid[x, y] = -1;
            }
        }

        mutationCooldowns = new float[MaxSpecies];
        speciesSpeciationCooldowns = new float[MaxSpecies];
        globalSpeciationTimer = 0f;
        cachedSpeciesBirths = new int[MaxSpecies];
        cachedSpeciesDeaths = new int[MaxSpecies];
        totalSpeciesBirths = new int[MaxSpecies];
        totalSpeciesDeaths = new int[MaxSpecies];

        previousSpeciesBirths = new int[MaxSpecies];
        previousSpeciesDeaths = new int[MaxSpecies];

        cachedSpeciesBirthRate = new float[MaxSpecies];
        cachedSpeciesDeathRate = new float[MaxSpecies];

        obstaclesDirty = true;

        UpdateFoodTexture();
        UpdateTrailTexture();
        UpdateObstacleTexture();
        UpdateAgentTexture();

        
        simulationPaused = true;
        worldAge = 0f;
    }

    private Color GetToolColor()
    {
        switch (currentTool)
        {
            case ToolMode.FoodPaint:
                return new Color(0.2f, 0.8f, 0.2f);

            case ToolMode.Obstacle:
                return Color.gray;

            case ToolMode.Erase:
                return Color.red;

            case ToolMode.Spawn:
                if (selectedSpeciesIndex >= 0 && selectedSpeciesIndex < activeSpeciesCount)
                    return runtimeSpecies[selectedSpeciesIndex].color;

                return Color.white;

            default:
                return Color.white;
        }
    }

    private void RefreshCachedStats()
    {
        if (agents == null)
            return;

        if (cachedSpeciesCounts == null || cachedSpeciesCounts.Length != activeSpeciesCount)
            cachedSpeciesCounts = new int[activeSpeciesCount];

        for (int i = 0; i < cachedSpeciesCounts.Length; i++)
            cachedSpeciesCounts[i] = 0;

        cachedTotalAlive = 0;

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            int speciesIndex = agents[i].speciesIndex;

            if (speciesIndex < 0 || speciesIndex >= activeSpeciesCount)
                continue;

            cachedSpeciesCounts[speciesIndex]++;
            cachedTotalAlive++;
        }

        GetFoodStats(out cachedTotalFood, out cachedFoodCoverage);
        cachedCorpseCount = GetActiveCorpseCount();
        RefreshEnergyAudit();
    }

    private void RefreshEnergyAudit()
    {
        cachedAgentEnergy = 0f;
        cachedGreenEnergy = 0f;
        cachedBrownEnergy = 0f;
        cachedCorpseEnergy = 0f;

        float[] speciesEnergy = new float[MaxSpecies];

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            float energy = Mathf.Max(0f, agents[i].energy);
            cachedAgentEnergy += energy;

            int s = agents[i].speciesIndex;
            if (s >= 0 && s < MaxSpecies)
                speciesEnergy[s] += energy;
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (foodTypes.Length > 0)
                    cachedGreenEnergy += foodGrid[x, y, 0];

                if (foodTypes.Length > 1)
                    cachedBrownEnergy += foodGrid[x, y, 1];
            }
        }

        for (int i = 0; i < corpses.Length; i++)
        {
            if (!corpses[i].active)
                continue;

            cachedCorpseEnergy += Mathf.Max(0f, corpses[i].energy);
        }

        cachedEcoEnergyTotal =
            cachedAgentEnergy +
            cachedGreenEnergy +
            cachedBrownEnergy +
            cachedCorpseEnergy;

        for (int i = 0; i < 3; i++)
        {
            cachedTopEnergySpecies[i] = -1;
            cachedTopEnergyAmounts[i] = 0f;
        }

        for (int s = 0; s < activeSpeciesCount; s++)
        {
            float e = speciesEnergy[s];

            for (int rank = 0; rank < 3; rank++)
            {
                if (e > cachedTopEnergyAmounts[rank])
                {
                    for (int move = 2; move > rank; move--)
                    {
                        cachedTopEnergyAmounts[move] = cachedTopEnergyAmounts[move - 1];
                        cachedTopEnergySpecies[move] = cachedTopEnergySpecies[move - 1];
                    }

                    cachedTopEnergyAmounts[rank] = e;
                    cachedTopEnergySpecies[rank] = s;
                    break;
                }
            }
        }
    }

    private void RecordPopulationHistory()
    {
        if (cachedSpeciesCounts == null)
            return;

        if (populationHistory == null ||
            populationHistory.GetLength(0) != MaxSpecies ||
            populationHistory.GetLength(1) != populationGraphSamples)
        {
            populationHistory = new int[MaxSpecies, populationGraphSamples];
            populationGraphIndex = 0;
        }

        int countLimit = Mathf.Min(activeSpeciesCount, cachedSpeciesCounts.Length, MaxSpecies);

        for (int s = 0; s < countLimit; s++)
        {
            populationHistory[s, populationGraphIndex] = cachedSpeciesCounts[s];
        }

        populationGraphIndex++;

        if (populationGraphIndex >= populationGraphSamples)
            populationGraphIndex = 0;
    }

    

    
}