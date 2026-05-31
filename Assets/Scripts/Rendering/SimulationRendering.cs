using UnityEngine;
using UnityEngine.InputSystem;

public partial class SimulationManager
{
    

    private void DrawBrushPreview()
    {
        if (!mouseToolsEnabled || Camera.main == null || Mouse.current == null)
            return;

        if (currentTool == ToolMode.None)
            return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(Camera.main.transform.position.z))
        );

        Vector3 center = new Vector3(mouseWorld.x, mouseWorld.y, 0f);

        Gizmos.color = GetToolColor();
        Gizmos.DrawWireSphere(center, brushRadius);
    }

    private void OnDrawGizmos()
    {
        DrawBrushPreview();
        DrawNestGizmos();
    }

    private void DrawNestGizmos()
    {
        if (nests == null || runtimeSpecies == null)
            return;

        for (int i = 0; i < nests.Length; i++)
        {
            if (!nests[i].active)
                continue;

            Color color = Color.white;

            int s = nests[i].speciesIndex;
            if (s >= 0 && s < activeSpeciesCount && runtimeSpecies[s] != null)
                color = runtimeSpecies[s].color;

            color.a = 1f;
            Gizmos.color = color;

            Gizmos.DrawWireSphere(nests[i].position, nestRadius);
            Gizmos.DrawSphere(nests[i].position, 0.35f);
        }
    }

}