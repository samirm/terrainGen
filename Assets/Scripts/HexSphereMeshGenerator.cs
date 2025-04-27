using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(HexasphereGenerator))]
public class HexSphereMeshGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform meshContainer;
    public Material defaultMaterial;
    
    [Header("Tile Materials")]
    public Material waterMaterial;
    public Material grassMaterial;
    public Material mountainMaterial;

    private HexasphereGenerator hexGenerator;

    void Start()
    {
        hexGenerator = GetComponent<HexasphereGenerator>();

        if (hexGenerator == null)
        {
            Debug.LogError("HexasphereGenerator component not found!");
            return;
        }

        if (hexGenerator.Tiles == null || hexGenerator.Tiles.Count == 0)
        {
            Debug.LogWarning("HexasphereGenerator has not generated tiles yet. Ensure it runs first (e.g., in Awake). Generating now...");
            // Optionally trigger generation if it hasn't run, ensure Awake runs first though
            hexGenerator.Generate();
             if (hexGenerator.Tiles == null || hexGenerator.Tiles.Count == 0) {
                 Debug.LogError("Failed to generate tiles even after manual trigger.");
                 return;
             }
        }

        if (meshContainer == null)
        {
             Debug.LogError("Mesh Container transform is not assigned in the Inspector!");
             // Attempt to find it by name as a fallback
             var containerGO = transform.Find("MeshContainer");
             if(containerGO != null) meshContainer = containerGO;
             else {
                 Debug.LogError("Could not find MeshContainer child object.");
                 return; // Stop if container is missing
             }
        }

         if (defaultMaterial == null)
        {
             Debug.LogWarning("Default Material not assigned. Using a default URP Lit material.");
             // Find the default URP Lit material (path might vary slightly with Unity version)
             defaultMaterial = Resources.Load<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit");
             // If still null, create a basic one (less ideal)
             if (defaultMaterial == null) defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        CreateAllTileMeshes();
    }

    void CreateAllTileMeshes()
    {
        Debug.Log($"Generating meshes for {hexGenerator.Tiles.Count} tiles...");

        // Optional: Clear previous meshes if regenerating
        foreach (Transform child in meshContainer) {
            Destroy(child.gameObject);
        }

        // --- Material Sanity Checks (Optional but Recommended) ---
        if (waterMaterial == null || grassMaterial == null || mountainMaterial == null) {
             Debug.LogError("One or more tile materials are not assigned in the Inspector!");
             // Assign fallback materials if needed (as done in Start previously)
             if (waterMaterial == null) waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.blue }; // Basic blue fallback
             if (grassMaterial == null) grassMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.green }; // Basic green fallback
             if (mountainMaterial == null) mountainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Color.grey }; // Basic grey fallback
        }


        for (int i = 0; i < hexGenerator.Tiles.Count; i++)
        {
            HexasphereGenerator.HexTile tile = hexGenerator.Tiles[i];
            // ... (Skip invalid tile check) ...

            // 1. Create GameObject
            GameObject tileGO = new GameObject($"Tile_{i}{(tile.IsPentagon ? "_P" : "_H")}_{tile.Type}"); // Add Type to name
            tileGO.transform.SetParent(meshContainer, false);
            tileGO.transform.position = tile.CenterPosition;

            // 2. Create the Mesh
            Mesh tileMesh = CreateSingleTileMesh(tile);

            // 3. Add MeshFilter
            MeshFilter meshFilter = tileGO.AddComponent<MeshFilter>();
            meshFilter.mesh = tileMesh;

            // 4. Add MeshRenderer
            MeshRenderer meshRenderer = tileGO.AddComponent<MeshRenderer>();

            // --- 5. Assign Material based on Tile Type --- <<< MODIFIED SECTION
            switch (tile.Type)
            {
                case TileType.Water:
                    meshRenderer.material = waterMaterial;
                    break;
                case TileType.Grass:
                    meshRenderer.material = grassMaterial;
                    break;
                case TileType.Mountain:
                    meshRenderer.material = mountainMaterial;
                    break;
                default: // Fallback in case new types are added later
                    meshRenderer.material = grassMaterial; // Or some default
                    break;
            }
        }
         Debug.Log("Finished generating tile meshes.");
        }

    Mesh CreateSingleTileMesh(HexasphereGenerator.HexTile tile)
    {
        Mesh mesh = new Mesh();
        mesh.name = $"Tile_{tile.Id}_Mesh";

        int cornerCount = tile.CornerVertices.Count;
        int vertexCount = cornerCount + 1; // Corners + Center

        // --- Vertices ---
        List<Vector3> vertices = new List<Vector3>(vertexCount);
        // Vertex 0 is the center (relative to the tile GameObject's position, so Vector3.zero)
        vertices.Add(Vector3.zero);
        // Add corner vertices relative to the center
        for (int i = 0; i < cornerCount; i++)
        {
            vertices.Add(tile.CornerVertices[i] - tile.CenterPosition); // Position relative to center
        }
        mesh.SetVertices(vertices);

        // --- Triangles ---
        // Create triangles fanning out from the center vertex (index 0)
        List<int> triangles = new List<int>(cornerCount * 3);
        for (int i = 0; i < cornerCount; i++)
        {
            triangles.Add(0); // Center vertex index
            triangles.Add(i + 1); // Current corner vertex index
            // Next corner vertex index (wrapping around using modulo)
            triangles.Add(((i + 1) % cornerCount) + 1);
        }
        mesh.SetTriangles(triangles, 0); // Submesh 0

        // --- Normals ---
        // For now, use the tile's center normal for all vertices (faceted look)
        List<Vector3> normals = new List<Vector3>(vertexCount);
        Vector3 normal = tile.CenterPosition.normalized;
        for (int i = 0; i < vertexCount; i++)
        {
            normals.Add(normal);
        }
        mesh.SetNormals(normals);

        // --- UVs (Basic Planar Mapping - adjust as needed) ---
        List<Vector2> uvs = new List<Vector2>(vertexCount);
        uvs.Add(new Vector2(0.5f, 0.5f)); // Center UV
        for (int i = 0; i < cornerCount; i++)
        {
            // Project corner onto a plane relative to center - very basic
            Vector3 relativePos = vertices[i+1]; // Already relative
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
             if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(normal, Vector3.forward).normalized; // Fallback
            Vector3 up = Vector3.Cross(right, normal).normalized;

            float u = Vector3.Dot(relativePos, right) * 0.5f + 0.5f; // Scale and center
            float v = Vector3.Dot(relativePos, up) * 0.5f + 0.5f;    // Scale and center
            uvs.Add(new Vector2(u, v));
        }
         mesh.SetUVs(0, uvs); // UV channel 0


        mesh.RecalculateBounds(); // Important for visibility culling

        return mesh;
    }
}
