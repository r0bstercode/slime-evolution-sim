using UnityEngine;

public partial class SimulationManager
{
    private void InitializeFoodTypes()
    {
        if (foodTypes != null && foodTypes.Length > 0)
            return;

        foodTypes = new FoodType[2];

        foodTypes[0] = new FoodType
        {
            foodName = "Green Biomass",
            color = new Color(0.27f, 0.55f, 0f),
            growthRate = foodGrowthRate,
            maxDensity = 15f,
            energyValue = 8f
        };

        foodTypes[1] = new FoodType
        {
            foodName = "Brown Decay",
            color = new Color(0.55f, 0.27f, 0.07f),
            growthRate = foodGrowthRate * 2f,
            maxDensity = 25f,
            energyValue = 0.8f
        };
    }

    private void InitializeFood()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                for (int f = 0; f < foodTypes.Length; f++)
                {
                    foodGrid[x, y, f] = 0f;
                }

                if (obstacleGrid[x, y])
                    continue;

                if (Random.value < initialFoodAmount)
                    foodGrid[x, y, 0] = foodTypes[0].maxDensity;
            }
        }
    }


    private void RegrowFood(float dt)
    {
        int stripes = Mathf.Max(1, foodUpdateStripes);
        int stripe = foodStripeIndex % stripes;
        foodStripeIndex++;

        float scaledDt = dt * stripes;

        float spreadChance = 0.1f * scaledDt;
        float rareSpawnChance = 0.000001f * scaledDt;

        float greenGrowthBudget = sunlightEnergyPerSecond * scaledDt;

        if (cachedEcoEnergyTotal >= maxEcoEnergy)
            greenGrowthBudget = 0f;

        int xStart = Mathf.FloorToInt(gridWidth * (stripe / (float)stripes));
        int xEnd = Mathf.FloorToInt(gridWidth * ((stripe + 1) / (float)stripes));

        for (int x = xStart; x < xEnd; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (obstacleGrid[x, y])
                    continue;

                // Green Biomass
                float green = foodGrid[x, y, 0];

                if (green > 0f)
                {
                    float growthAmount =
                        foodTypes[0].growthRate * scaledDt +
                        foodGrid[x, y, 1] * decayGrowthBoost * scaledDt;

                    growthAmount = Mathf.Min(growthAmount, greenGrowthBudget);
                    greenGrowthBudget -= growthAmount;

                    foodGrid[x, y, 0] = Mathf.Min(
                        foodTypes[0].maxDensity,
                        green + growthAmount
                    );

                    if (foodGrid[x, y, 0] >= foodTypes[0].maxDensity &&
                        Random.value < spreadChance)
                    {
                        TrySpreadFoodFrom(x, y, 0);
                    }
                }
                else if (Random.value < rareSpawnChance)
                {
                    foodGrid[x, y, 0] = foodTypes[0].maxDensity * 0.1f;
                }

                // Brown Decay
                float brown = foodGrid[x, y, 1];

                if (brown > 0f)
                {
                    foodGrid[x, y, 1] = Mathf.Max(
                        0f,
                        brown - brownDecayLoss * scaledDt
                    );

                    if (foodGrid[x, y, 1] >= foodTypes[1].maxDensity &&
                        Random.value < spreadChance)
                    {
                        TrySpreadFoodFrom(x, y, 1);
                    }
                }
                else if (Random.value < rareSpawnChance)
                {
                    foodGrid[x, y, 1] = foodTypes[1].maxDensity * 0.1f;
                }
            }
        }
    }

    private void TrySpreadFoodFrom(int x, int y, int foodTypeIndex)
    {
        int direction = Random.Range(0, 4);

        int nx = x;
        int ny = y;

        if (direction == 0) nx++;
        else if (direction == 1) nx--;
        else if (direction == 2) ny++;
        else if (direction == 3) ny--;

        if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight)
            return;

        if (obstacleGrid[nx, ny])
            return;

        FoodType foodType = foodTypes[foodTypeIndex];

        if (foodGrid[nx, ny, foodTypeIndex] <= 0f)
            foodGrid[nx, ny, foodTypeIndex] = foodType.maxDensity * 0.05f;
    }

    private void UpdateCorpses(float dt)
    {
        if (corpses == null)
            return;

        for (int i = 0; i < corpses.Length; i++)
        {
            if (!corpses[i].active)
                continue;

            Corpse corpse = corpses[i];

            float decayAmount = corpseDecayRate * dt;
            float converted = Mathf.Min(decayAmount, corpse.energy);

            if (converted > 0f)
            {
                if (!WorldToGrid(corpse.position, out int gx, out int gy))
                    continue;

                if (foodTypes.Length > 1 && !obstacleGrid[gx, gy])
                    foodGrid[gx, gy, 1] += converted;
                corpse.energy -= converted;

                if (WorldToGrid(corpse.position, out int cgx, out int cgy))
                {
                    corpseGrid[cgx, cgy] = Mathf.Max(
                        0f,
                        corpseGrid[cgx, cgy] - converted
                    );
                }
            }

            corpse.age += dt;

            if (corpse.energy <= 0.01f)
                corpse.active = false;

            corpses[i] = corpse;
        }
    }

    private void SpawnCorpse(Vector2 worldPos, float sourceEnergy, int speciesIndex)
    {
        if (sourceEnergy <= 0f)
            return;

        for (int i = 0; i < corpses.Length; i++)
        {
            if (corpses[i].active)
                continue;

            corpses[i] = new Corpse
            {
                active = true,
                position = worldPos,
                energy = sourceEnergy,
                age = 0f,
                speciesIndex = speciesIndex
            };

            if (WorldToGrid(worldPos, out int gx, out int gy))
            {
                corpseGrid[gx, gy] += sourceEnergy;
            }

            return;
        }
    }

    private float GetCorpseSignalAt(Vector2 sensorPosition, RuntimeSpecies dna)
    {
        if (!WorldToGrid(sensorPosition, out int x, out int y))
            return 0f;

        return corpseGrid[x, y] * dna.corpsePreference;
    }

    private void InitializeObstacles()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                obstacleGrid[x, y] = false;
            }
        }

        if (randomObstaclesEnabled)
            GenerateRandomObstacleBlobs();
    }

    private void DepositDanger(SlimeAgent agent, RuntimeSpecies dna, float dt)
    {
        if (dna.preyPreference < predatorThreshold)
            return;

        if (!WorldToGrid(agent.position, out int x, out int y))
            return;

        if (obstacleGrid[x, y])
            return;

        dangerGrid[x, y] += dangerDeposit * dt;
    }

    private void GenerateRandomObstacleBlobs()
    {
        for (int i = 0; i < randomObstacleBlobCount; i++)
        {
            int centerX = Random.Range(0, gridWidth);
            int centerY = Random.Range(0, gridHeight);

            int radius = Random.Range(
                Mathf.Max(2, randomObstacleBlobRadius / 2),
                randomObstacleBlobRadius + 1
            );

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                        continue;

                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(centerX, centerY)
                    );

                    float noise = Random.Range(-2f, 2f);

                    if (distance + noise <= radius)
                        obstacleGrid[x, y] = true;
                }
            }
        }
    }

        private void ClearObstacles()
         {
            if (obstacleGrid == null)
            return;

            for (int x = 0; x < gridWidth; x++)
            {
            for (int y = 0; y < gridHeight; y++)
            {
                obstacleGrid[x, y] = false;
            }
        }
    }

    private void AddRandomObstacles()
    {
        if (obstacleGrid == null)
            obstacleGrid = new bool[gridWidth, gridHeight];

        GenerateRandomObstacleBlobs();
        obstaclesDirty = true;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!obstacleGrid[x, y])
                    continue;

                for (int f = 0; f < foodTypes.Length; f++)
                    foodGrid[x, y, f] = 0f;

                for(int s = 0; s < activeSpeciesCount; s++)
                    trailGrid[x, y, s] = 0f;
            }
        }
    }

    private void RegenerateObstaclesOnly()
    {
        obstacleGrid = new bool[gridWidth, gridHeight];
        InitializeObstacles();
        obstaclesDirty = true;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!obstacleGrid[x, y])
                    continue;

                for (int f = 0; f < foodTypes.Length; f++)
                    foodGrid[x, y, f] = 0f;

                for (int s = 0; s < activeSpeciesCount; s++)
                    trailGrid[x, y, s] = 0f;
            }
        }
    }

    private bool IsObstacleAt(Vector2 worldPosition)
    {
        if (obstacleGrid == null)
            return false;

        if (!WorldToGrid(worldPosition, out int x, out int y))
            return true;

        return obstacleGrid[x, y];
    }

    private void DiffuseAndDecayTrails(float dt)
    {
        if (activeSpeciesCount <= 0)
            return;

        int s = trailSpeciesUpdateIndex % activeSpeciesCount;
        trailSpeciesUpdateIndex++;

        float scaledDt = dt * activeSpeciesCount;

        float decay = trailDecayRate * scaledDt;
        float diffusion = trailDiffusionRate * scaledDt;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (obstacleGrid[x, y])
                {
                    nextTrailGrid[x, y, s] = 0f;
                    continue;
                }

                float center = trailGrid[x, y, s];

                bool hasNearbyTrail = center > 0.0001f;

                if (!hasNearbyTrail)
                {
                    if (x > 0 && trailGrid[x - 1, y, s] > 0.0001f)
                        hasNearbyTrail = true;
                    else if (x < gridWidth - 1 && trailGrid[x + 1, y, s] > 0.0001f)
                        hasNearbyTrail = true;
                    else if (y > 0 && trailGrid[x, y - 1, s] > 0.0001f)
                        hasNearbyTrail = true;
                    else if (y < gridHeight - 1 && trailGrid[x, y + 1, s] > 0.0001f)
                        hasNearbyTrail = true;
                }

                if (!hasNearbyTrail)
                {
                    nextTrailGrid[x, y, s] = 0f;
                    continue;
                }

                float sum = center;
                int count = 1;

                if (x > 0 && !obstacleGrid[x - 1, y])
                {
                    sum += trailGrid[x - 1, y, s];
                    count++;
                }

                if (x < gridWidth - 1 && !obstacleGrid[x + 1, y])
                {
                    sum += trailGrid[x + 1, y, s];
                    count++;
                }

                if (y > 0 && !obstacleGrid[x, y - 1])
                {
                    sum += trailGrid[x, y - 1, s];
                    count++;
                }

                if (y < gridHeight - 1 && !obstacleGrid[x, y + 1])
                {
                    sum += trailGrid[x, y + 1, s];
                    count++;
                }

                float blurred = sum / count;
                float diffused = Mathf.Lerp(center, blurred, diffusion);
                nextTrailGrid[x, y, s] = Mathf.Max(0f, diffused - decay);
            }
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                trailGrid[x, y, s] = nextTrailGrid[x, y, s];
            }
        }
    }

    private bool WorldToGrid(Vector2 worldPosition, out int x, out int y)
    {
        float normalizedX = (worldPosition.x + simulationArea.x * 0.5f) / simulationArea.x;
        float normalizedY = (worldPosition.y + simulationArea.y * 0.5f) / simulationArea.y;

        x = Mathf.FloorToInt(normalizedX * gridWidth);
        y = Mathf.FloorToInt(normalizedY * gridHeight);

        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    private Vector2 GridToWorld(int x, int y)
    {
        float cellWidth = simulationArea.x / gridWidth;
        float cellHeight = simulationArea.y / gridHeight;

        return new Vector2(
            -simulationArea.x * 0.5f + x * cellWidth + cellWidth * 0.5f,
            -simulationArea.y * 0.5f + y * cellHeight + cellHeight * 0.5f
        );
    }

    private float GetTotalFoodAt(int x, int y)
    {
        float total = 0f;

        for (int f = 0; f < foodTypes.Length; f++)
        {
            total += foodGrid[x, y, f];
        }

        return total;
    }

    private void GetFoodStats(out float totalFood, out float coveragePercent)
    {
        totalFood = 0f;
        int foodCells = 0;
        int validCells = 0;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (obstacleGrid != null && obstacleGrid[x, y])
                    continue;

                validCells++;

                float cellFood = GetTotalFoodAt(x, y);
                totalFood += cellFood;

                if (cellFood > 0.01f)
                    foodCells++;
            }
        }

        coveragePercent = validCells > 0
            ? (foodCells / (float)validCells) * 100f
            : 0f;
    }

    private float GetFoodSharePercent(int foodTypeIndex)
    {
        if (foodGrid == null || foodTypes == null)
            return 0f;

        float typeAmount = 0f;
        float totalAmount = 0f;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (obstacleGrid != null && obstacleGrid[x, y])
                    continue;

                for (int f = 0; f < foodTypes.Length; f++)
                {
                    float amount = foodGrid[x, y, f];
                    totalAmount += amount;

                    if (f == foodTypeIndex)
                        typeAmount += amount;
                }
            }
        }

        if (totalAmount <= 0f)
            return 0f;

        return (typeAmount / totalAmount) * 100f;
    }

    private float GetTotalMaxFoodDensity()
    {
        float total = 0f;

        for (int f = 0; f < foodTypes.Length; f++)
            total += foodTypes[f].maxDensity;

        return total;
    }

    private int GetActiveCorpseCount()
    {
        if (corpses == null)
            return 0;

        int count = 0;

        for (int i = 0; i < corpses.Length; i++)
        {
            if (corpses[i].active)
                count++;
        }

        return count;
    }

    private void EatCorpse(ref SlimeAgent agent, RuntimeSpecies dna, float dt)
    {
        float energyRatio = agent.energy / dna.energyCapacity;

        if (energyRatio >= dna.satiationThreshold)
            return;

        if (dna.corpsePreference < corpseScavengerThreshold)
            return;

        if (!WorldToGrid(agent.position, out int x, out int y))
            return;

        //if (IsOwnCorpseAt(agent.position, agent.speciesIndex))
        //    return;

        float available = corpseGrid[x, y];

        if (available <= 0.01f)
            return;

        float eaten = Mathf.Min(
            corpseEatAmount * dt,
            available
        );

        corpseGrid[x, y] = Mathf.Max(
            0f,
            corpseGrid[x, y] - eaten
        );

        agent.energy = Mathf.Min(
            dna.energyCapacity,
            agent.energy + eaten * corpseEnergyValue
        );

        agent.pauseTimer = Random.Range(
            dna.eatPauseMin,
            dna.eatPauseMax
        );
    }

    private bool IsOwnCorpseAt(Vector2 position, int speciesIndex)
    {
        for (int i = 0; i < corpses.Length; i++)
        {
            if (!corpses[i].active)
                continue;

            if (corpses[i].speciesIndex != speciesIndex)
                continue;

            if (Vector2.Distance(position, corpses[i].position) <= 0.6f)
                return true;
        }

        return false;
    }

    private void DiffuseAndDecayDanger(float dt)
    {
        if (dangerGrid == null || nextDangerGrid == null)
            return;

        float decay = dangerDecay * dt;
        float diffusion = dangerDiffusion * dt;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (obstacleGrid[x, y])
                {
                    nextDangerGrid[x, y] = 0f;
                    continue;
                }

                float center = dangerGrid[x, y];

                bool hasNearbyDanger = center > 0.0001f;

                if (!hasNearbyDanger)
                {
                    if (x > 0 && dangerGrid[x - 1, y] > 0.0001f)
                        hasNearbyDanger = true;
                    else if (x < gridWidth - 1 && dangerGrid[x + 1, y] > 0.0001f)
                        hasNearbyDanger = true;
                    else if (y > 0 && dangerGrid[x, y - 1] > 0.0001f)
                        hasNearbyDanger = true;
                    else if (y < gridHeight - 1 && dangerGrid[x, y + 1] > 0.0001f)
                        hasNearbyDanger = true;
                }

                if (!hasNearbyDanger)
                {
                    nextDangerGrid[x, y] = 0f;
                    continue;
                }

                float sum = center;
                int count = 1;

                if (x > 0 && !obstacleGrid[x - 1, y])
                {
                    sum += dangerGrid[x - 1, y];
                    count++;
                }

                if (x < gridWidth - 1 && !obstacleGrid[x + 1, y])
                {
                    sum += dangerGrid[x + 1, y];
                    count++;
                }

                if (y > 0 && !obstacleGrid[x, y - 1])
                {
                    sum += dangerGrid[x, y - 1];
                    count++;
                }

                if (y < gridHeight - 1 && !obstacleGrid[x, y + 1])
                {
                    sum += dangerGrid[x, y + 1];
                    count++;
                }

                float blurred = sum / count;
                float diffused = Mathf.Lerp(center, blurred, diffusion);

                nextDangerGrid[x, y] = Mathf.Max(0f, diffused - decay);
            }
        }

        float[,] temp = dangerGrid;
        dangerGrid = nextDangerGrid;
        nextDangerGrid = temp;
    }

    private float GetDangerSignalAt(Vector2 sensorPosition, RuntimeSpecies dna)
    {
        if (dangerGrid == null)
            return 0f;

        if (dna.preyPreference >= predatorThreshold)
            return 0f;

        if (!WorldToGrid(sensorPosition, out int x, out int y))
            return 0f;

        return dangerGrid[x, y] * preyDangerAvoidance;
    }
}