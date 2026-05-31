using UnityEngine;

public partial class SimulationManager
{
    private Texture2D foodTexture;
    private SpriteRenderer foodRenderer;
    private Texture2D trailTexture;
    private SpriteRenderer trailRenderer;
    private Color[] foodPixels;
    private Color[] trailPixels;
    private Texture2D obstacleTexture;
    private SpriteRenderer obstacleRenderer;
    private Color[] obstaclePixels;

    private Texture2D agentTexture;
    private SpriteRenderer agentRenderer;
    private Color[] agentPixels;

    private bool obstaclesDirty = true;

    private void InitializeAgentTextureRenderer()
    {
        agentTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
        agentTexture.filterMode = FilterMode.Point;
        agentTexture.wrapMode = TextureWrapMode.Clamp;

        agentPixels = new Color[gridWidth * gridHeight];

        GameObject obj = new GameObject("Agent Texture Renderer");
        obj.transform.parent = transform;
        obj.transform.position = Vector3.zero;

        agentRenderer = obj.AddComponent<SpriteRenderer>();

        Sprite sprite = Sprite.Create(
            agentTexture,
            new Rect(0, 0, gridWidth, gridHeight),
            new Vector2(0.5f, 0.5f),
            gridWidth / simulationArea.x
        );

        agentRenderer.sprite = sprite;
        agentRenderer.sortingOrder = 999;
    }

    private void UpdateAgentTexture()
    {
        if (agentTexture == null)
            InitializeAgentTextureRenderer();

        if (agentTexture == null || agentPixels == null || agents == null || runtimeSpecies == null)
            return;

        for (int i = 0; i < agentPixels.Length; i++)
            agentPixels[i] = Color.clear;

        for (int i = 0; i < agents.Length; i++)
        {
            if (!agents[i].alive)
                continue;

            if (!WorldToGrid(agents[i].position, out int x, out int y))
                continue;

            int speciesIndex = agents[i].speciesIndex;

            if (speciesIndex < 0 || speciesIndex >= activeSpeciesCount)
                continue;

            RuntimeSpecies dna = runtimeSpecies[speciesIndex];

            if (dna == null)
                continue;

            Color color = dna.color;
            color.a = 1f;

            int idx = y * gridWidth + x;
            agentPixels[idx] = color;
        }

        agentTexture.SetPixels(agentPixels);
        agentTexture.Apply(false);
    }

    private void InitializeFoodRenderer()
    {
        foodTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
        foodTexture.filterMode = FilterMode.Bilinear;
        foodTexture.wrapMode = TextureWrapMode.Clamp;
        foodPixels = new Color[gridWidth * gridHeight];

        GameObject obj = new GameObject("Food Texture Renderer");
        obj.transform.parent = transform;
        obj.transform.position = Vector3.zero;

        foodRenderer = obj.AddComponent<SpriteRenderer>();

        Sprite sprite = Sprite.Create(
            foodTexture,
            new Rect(0, 0, gridWidth, gridHeight),
            new Vector2(0.5f, 0.5f),
            gridWidth / simulationArea.x
        );

        foodRenderer.sprite = sprite;
        foodRenderer.sortingOrder = -10;
    }

    private void UpdateFoodTexture()
    {
        if (foodTexture == null || foodGrid == null || foodTypes == null)
            return;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int index = y * gridWidth + x;

                float green = foodGrid[x, y, 0] / foodTypes[0].maxDensity;
                float brown = foodGrid[x, y, 1] / foodTypes[1].maxDensity;
                float corpse = 0f;

                if (corpseGrid != null)
                    corpse = corpseGrid[x, y] / 5f;

                corpse = Mathf.Clamp01(corpse);

                green = Mathf.Clamp01(green);
                brown = Mathf.Clamp01(brown);

                Color color = Color.black;

                if (green > 0.01f)
                    color += Color.green * green;

                if (brown > 0.01f)
                    color += new Color(0.55f, 0.27f, 0.07f) * brown;

                if (corpse > 0.01f)
                    color += new Color(0.9f, 0.1f, 0.1f) * corpse;

                color.a = 1f;

                foodPixels[index] = color;
            }
        }

        foodTexture.SetPixels(foodPixels);
        foodTexture.Apply(false);
    }

    private void InitializeTrailRenderer()
    {
        trailTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
        trailTexture.filterMode = FilterMode.Bilinear;
        trailTexture.wrapMode = TextureWrapMode.Clamp;
        trailPixels = new Color[gridWidth * gridHeight];

        GameObject obj = new GameObject("Trail Texture Renderer");
        obj.transform.parent = transform;
        obj.transform.position = Vector3.zero;

        trailRenderer = obj.AddComponent<SpriteRenderer>();

        Sprite sprite = Sprite.Create(
            trailTexture,
            new Rect(0, 0, gridWidth, gridHeight),
            new Vector2(0.5f, 0.5f),
            gridWidth / simulationArea.x
        );

        trailRenderer.sprite = sprite;
        trailRenderer.sortingOrder = -5;
    }

    private void UpdateTrailTexture()
    {
        if (!showTrails)
        {
            for (int i = 0; i < trailPixels.Length; i++)
                trailPixels[i] = Color.clear;

            trailTexture.SetPixels(trailPixels);
            trailTexture.Apply(false);
            return;
        }

        if (trailTexture == null || trailGrid == null || runtimeSpecies == null)
            return;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int index = y * gridWidth + x;

                //int dominantSpecies = -1;
                //float strongestTrail = 0f;

                //for (int s = 0; s < activeSpeciesCount; s++)
                //{
                //    float trail = trailGrid[x, y, s];

                //    if (trail > strongestTrail)
                //    {
                //        strongestTrail = trail;
                //        dominantSpecies = s;
                //    }
                //}

                //if (dominantSpecies < 0 || strongestTrail <= 0.001f)
                //{
                //    trailPixels[index] = Color.clear;
                //    continue;
                //}

                //Color color = runtimeSpecies[dominantSpecies].color;
                //color.a = Mathf.Clamp01(strongestTrail * trailAlpha);

                float homeTrail = 0f;
                float foodTrail = 0f;

                for (int s = 0; s < activeSpeciesCount; s++)
                {
                    if (homeTrailGrid != null)
                        homeTrail = Mathf.Max(homeTrail, homeTrailGrid[x, y, s]);

                    if (foodTrailGrid != null)
                        foodTrail = Mathf.Max(foodTrail, foodTrailGrid[x, y, s]);
                }

                Color color = Color.clear;

                if (homeTrail > 0.005f)
                    color += Color.blue * Mathf.Clamp01(homeTrail * 4f);

                if (foodTrail > 0.005f)
                    color += Color.yellow * Mathf.Clamp01(foodTrail * 4f);

                color.a = Mathf.Clamp01(
                    Mathf.Max(homeTrail, foodTrail) * 3f
                );

                trailPixels[index] = color;
            }
        }

        trailTexture.SetPixels(trailPixels);
        trailTexture.Apply(false);
    }

    private void InitializeObstacleRenderer()
    {
        obstacleTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false);
        obstacleTexture.filterMode = FilterMode.Point;
        obstacleTexture.wrapMode = TextureWrapMode.Clamp;

        obstaclePixels = new Color[gridWidth * gridHeight];

        GameObject obj = new GameObject("Obstacle Texture Renderer");
        obj.transform.parent = transform;
        obj.transform.position = Vector3.zero;

        obstacleRenderer = obj.AddComponent<SpriteRenderer>();

        Sprite sprite = Sprite.Create(
            obstacleTexture,
            new Rect(0, 0, gridWidth, gridHeight),
            new Vector2(0.5f, 0.5f),
            gridWidth / simulationArea.x
        );

        obstacleRenderer.sprite = sprite;
        obstacleRenderer.sortingOrder = -3;

        obstaclesDirty = true;
    }

    private void UpdateObstacleTexture()
    {
        if (!obstaclesDirty)
            return;

        if (obstacleTexture == null || obstacleGrid == null)
            return;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int index = y * gridWidth + x;

                obstaclePixels[index] = obstacleGrid[x, y]
                    ? new Color(0.12f, 0.12f, 0.12f, 1f)
                    : Color.clear;
            }
        }

        obstacleTexture.SetPixels(obstaclePixels);
        obstacleTexture.Apply(false);

        obstaclesDirty = false;
    }

}