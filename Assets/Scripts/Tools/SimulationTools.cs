using UnityEngine;
using UnityEngine.InputSystem;

public partial class SimulationManager
{
    private void HandleHotkeys()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
            ToggleTool(ToolMode.FoodPaint);

        if (Keyboard.current.wKey.wasPressedThisFrame)
            ToggleTool(ToolMode.Obstacle);

        if (Keyboard.current.eKey.wasPressedThisFrame)
            ToggleTool(ToolMode.Erase);

        if (Keyboard.current.rKey.wasPressedThisFrame)
            ToggleTool(ToolMode.Spawn);

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            selectedSpeciesIndex = 0;

        if (Keyboard.current.digit2Key.wasPressedThisFrame && activeSpeciesCount > 1)
            selectedSpeciesIndex = 1;

        if (Keyboard.current.digit3Key.wasPressedThisFrame && activeSpeciesCount > 2)
            selectedSpeciesIndex = 2;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            currentTool = ToolMode.None;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            simulationPaused = !simulationPaused;

        if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            ResetSimulation();
    }

    private void ToggleTool(ToolMode tool)
    {
        currentTool = currentTool == tool ? ToolMode.None : tool;
    }

    private void HandleBrushSize()
    {
        if (Mouse.current == null)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        brushRadius += Mathf.Sign(scroll) * brushScrollSpeed;
        brushRadius = Mathf.Clamp(brushRadius, 0.5f, 20f);
    }

    private void HandleMouseTools()
    {
        if (!mouseToolsEnabled)
            return;

        if (Camera.main == null || Mouse.current == null)
            return;

        if (currentTool == ToolMode.None)
            return;

        bool left = Mouse.current.leftButton.isPressed;
        bool right = Mouse.current.rightButton.wasPressedThisFrame;

        if (right)
        {
            currentTool = ToolMode.None;
            return;
        }

        if (!left)
            return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, Camera.main.nearClipPlane)
        );

        Vector2 mousePos = new Vector2(mouseWorld.x, mouseWorld.y);

        switch (currentTool)
        {
            case ToolMode.FoodPaint:
                PaintFood(mousePos, brushRadius, brushStrength);
                break;

            case ToolMode.Obstacle:
                PaintObstacle(mousePos, brushRadius);
                break;

            case ToolMode.Erase:
                EraseAt(mousePos, brushRadius);
                break;

            case ToolMode.Spawn:
                SpawnAgentsAt(mousePos, selectedSpeciesIndex, spawnCountPerClick, spawnRadius);
                break;
        }
    }

    private void PaintFood(Vector2 worldPos, float radius, float strength)
    {
        ApplyBrush(worldPos, radius, (x, y, falloff) =>
        {
            if (obstacleGrid[x, y])
                return;

            int foodTypeIndex = selectedFoodType;

            if (foodTypeIndex < 0 || foodTypeIndex >= foodTypes.Length)
                return;

            FoodType foodType = foodTypes[foodTypeIndex];

            foodGrid[x, y, foodTypeIndex] = Mathf.Min(
                foodType.maxDensity,
                foodGrid[x, y, foodTypeIndex] + strength * falloff
            );
        });
    }

    private void PaintObstacle(Vector2 worldPos, float radius)
    {
        ApplyBrush(worldPos, radius, (x, y, falloff) =>
        {
            obstacleGrid[x, y] = true;
            obstaclesDirty = true;

            for (int f = 0; f < foodTypes.Length; f++)
                foodGrid[x, y, f] = 0f;

            for (int s = 0; s < activeSpeciesCount; s++)
                trailGrid[x, y, s] = 0f;
        });
    }

    private void EraseAt(Vector2 worldPos, float radius)
    {
        ApplyBrush(worldPos, radius, (x, y, falloff) =>
        {
            obstacleGrid[x, y] = false;
            obstaclesDirty = true;

            for (int f = 0; f < foodTypes.Length; f++)
                foodGrid[x, y, f] = 0f;

            for (int s = 0; s < activeSpeciesCount; s++)
                trailGrid[x, y, s] = 0f;
        });
    }

    private void ApplyBrush(Vector2 worldPos, float radius, System.Action<int, int, float> action)
    {
        float cellWidth = simulationArea.x / gridWidth;
        float cellHeight = simulationArea.y / gridHeight;

        int radiusX = Mathf.CeilToInt(radius / cellWidth);
        int radiusY = Mathf.CeilToInt(radius / cellHeight);

        if (!WorldToGrid(worldPos, out int centerX, out int centerY))
            return;

        for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
        {
            for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
            {
                if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                    continue;

                Vector2 cellWorld = GridToWorld(x, y);
                float distance = Vector2.Distance(worldPos, cellWorld);

                if (distance > radius)
                    continue;

                float falloff = 1f - distance / radius;
                action(x, y, falloff);
            }
        }
    }



    private void SpawnAgentsAt(Vector2 center, int speciesIndex, int count, float radius)
    {
        if (speciesIndex < 0 || speciesIndex >= activeSpeciesCount)
            return;

        RuntimeSpecies dna = runtimeSpecies[speciesIndex];
        int spawned = 0;

        for (int i = 0; i < agents.Length && spawned < count; i++)
        {
            if (agents[i].alive)
                continue;

            Vector2 position = center + Random.insideUnitCircle * radius;

            if (IsObstacleAt(position))
                continue;

            agents[i] = new SlimeAgent
            {
                position = position,
                angle = Random.Range(0f, 360f),
                speciesIndex = speciesIndex,
                age = 0f,
                energy = runtimeSpecies[speciesIndex].startEnergy,
                alive = true,
                lockedCorpseIndex = -1,
                lockedFoodX = -1,
                lockedFoodY = -1,
                lockedFoodType = -1,
                pauseTimer = 0f,
                hp = 1f,
                slowTimer = 0f,
                slowMultiplier = 1f,
                mode = AgentMode.Foraging,
                homeNestIndex = -1,
                foodTrailTimer = 0f,
                foodTrailStrength = 0f,


                cachedLeftSense = 0f,
                cachedForwardSense = 0f,
                cachedRightSense = 0f,
                senseCacheValid = false
            };

            spawned++;
        }
    }
}