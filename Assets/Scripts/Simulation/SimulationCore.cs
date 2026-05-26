using UnityEngine;

public partial class SimulationManager
{
    private void InitializeAgents()
    {
        agents = new SlimeAgent[startingAgentCount];

        int agentIndex = 0;

        for (int speciesIndex = 0; speciesIndex < activeSpeciesCount; speciesIndex++)
        {
            RuntimeSpecies dna = runtimeSpecies[speciesIndex];

            int startCount = startingPopulationPerSpecies;

            if (dna.sourceDNA != null)
                startCount = dna.sourceDNA.startingPopulation;

            for (int i = 0; i < startCount; i++)
            {
                if (agentIndex >= agents.Length)
                    return;

                agents[agentIndex] = new SlimeAgent
                {
                    position = GetRandomFreePosition(),
                    angle = Random.Range(0f, 360f),
                    speciesIndex = speciesIndex,
                    age = 0f,
                    energy = dna.startEnergy,
                    alive = true,
                    pauseTimer = 0f
                };

                agentIndex++;
            }
        }
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

    private void ResetSimulation()
    {
        trailGrid = new float[gridWidth, gridHeight, MaxSpecies];
        nextTrailGrid = new float[gridWidth, gridHeight, MaxSpecies];

        obstacleGrid = new bool[gridWidth, gridHeight];
        InitializeObstacles();

        foodGrid = new float[gridWidth, gridHeight, foodTypes.Length];
        InitializeFood();
        InitializeFoodRenderer();
        InitializeTrailRenderer();
        InitializeObstacleRenderer();
        InitializeAgentTextureRenderer();
        //InitializeAgentRenderer();
        dangerGrid = new float[gridWidth, gridHeight];
        nextDangerGrid = new float[gridWidth, gridHeight];
        InitializeAgents();
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

        usedPreyCells = new Vector2Int[gridWidth * gridHeight];
        preyCellUsed = new bool[gridWidth, gridHeight];
        usedPreyCellCount = 0;

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

        simulationPaused = true;
        worldAge = 0f;
    }

    private void SpawnDeathDecay(Vector2 worldPos, float sourceEnergy)
    {
        if (foodGrid == null || foodTypes == null || foodTypes.Length < 2)
            return;

        float decayEnergy = sourceEnergy * deathDecayConversion;

        if (decayEnergy <= 0f)
            return;

        if (!WorldToGrid(worldPos, out int gx, out int gy))
            return;

        if (obstacleGrid != null && obstacleGrid[gx, gy])
            return;

        foodGrid[gx, gy, 1] += decayEnergy;
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
                    cachedGreenEnergy += foodGrid[x, y, 0] * foodTypes[0].energyValue;

                if (foodTypes.Length > 1)
                    cachedBrownEnergy += foodGrid[x, y, 1] * foodTypes[1].energyValue;
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