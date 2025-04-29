using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HexSphereGenerator))]
public class HexSphereMeshGenerator : MonoBehaviour
{
    private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
    [Header("References")] public Transform meshContainer;

    public Material defaultMaterial;

    [Header("Tile Materials")] 
    public Material waterMaterial;
    public Material grassMaterial;
    public Material hillMaterial;
    public Material mountainMaterial;
    public Material terrainMaterial;
    public Gradient heightGradient;
    public float visualHeightScale = .05f;
    private HexSphereGenerator _hexGenerator;
    private MaterialPropertyBlock _materialPropertyBlock;

    private void Awake()
    {
        _hexGenerator = GetComponent<HexSphereGenerator>();
        _materialPropertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        _hexGenerator = GetComponent<HexSphereGenerator>();

        if (_hexGenerator == null)
        {
            Debug.LogError("HexasphereGenerator component not found!");
            return;
        }

        if (_hexGenerator.Tiles == null || _hexGenerator.Tiles.Count == 0)
        {
            Debug.LogWarning(
                "HexasphereGenerator has not generated tiles yet. Ensure it runs first (e.g., in Awake). Generating now...");
            // Optionally trigger generation if it hasn't run, ensure Awake runs first though
            _hexGenerator.Generate();
            if (_hexGenerator.Tiles == null || _hexGenerator.Tiles.Count == 0)
            {
                Debug.LogError("Failed to generate tiles even after manual trigger.");
                return;
            }
        }

        if (meshContainer == null)
        {
            Debug.LogError("Mesh Container transform is not assigned in the Inspector!");
            // Attempt to find it by name as a fallback
            var containerGo = transform.Find("MeshContainer");
            if (containerGo != null)
            {
                meshContainer = containerGo;
            }
            else
            {
                Debug.LogError("Could not find MeshContainer child object.");
                return; // Stop if container is missing
            }
        }

        if (terrainMaterial == null)
        {
            Debug.LogError("Terrain Material not assigned!");
            terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = Color.gray }; // Fallback white
        }

        if (heightGradient == null || heightGradient.colorKeys.Length < 2)
        {
            Debug.LogWarning("Height Gradient not configured. Using default grey scale.");
            // Create a default gradient if none provided
            heightGradient = new Gradient();
            heightGradient.SetKeys(
                new GradientColorKey[]
                    { new(Color.black, 0.0f), new(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
            );
        }

        CreateAllTileMeshes();
    }

    private void CreateAllTileMeshes()
    {
        Debug.Log($"Generating meshes for {_hexGenerator.Tiles.Count} tiles...");

        // Optional: Clear previous meshes if regenerating
        foreach (Transform child in meshContainer) Destroy(child.gameObject);

        // --- Material Sanity Checks (Optional but Recommended) ---
        if (waterMaterial == null || grassMaterial == null || mountainMaterial == null || hillMaterial == null)
        {
            Debug.LogError("One or more tile materials are not assigned in the Inspector!");
            // Assign fallback materials if needed (as done in Start previously)
            if (waterMaterial == null)
                waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = Color.blue }; // Basic blue fallback
            if (grassMaterial == null)
                grassMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = Color.green }; // Basic green fallback
            if (hillMaterial == null)
                hillMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = Color.yellow }; // Basic yellow fallback
            if (mountainMaterial == null)
                mountainMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { color = Color.white }; // Basic grey fallback
        }


        for (var i = 0; i < _hexGenerator.Tiles.Count; i++)
        {
            var tile = _hexGenerator.Tiles[i];
            // ... (Skip invalid tile check) ...

            // 1. Create GameObject
            var tileGo = new GameObject($"Tile_{i}_Lvl{tile.heightLevel}{(tile.isPentagon ? "_P" : "_H")}_{tile.type}");
            tileGo.transform.SetParent(meshContainer, false);
            tileGo.transform.position = tile.centerPosition;

            // 2. Create the Mesh
            var tileMesh = CreateSingleTileMesh(tile);

            // 3. Add MeshFilter
            var meshFilter = tileGo.AddComponent<MeshFilter>();
            meshFilter.mesh = tileMesh;

            // 4. Add MeshRenderer
            var meshRenderer = tileGo.AddComponent<MeshRenderer>();
            meshRenderer.material = terrainMaterial;

            // 5. Assign Material based on Tile Type ---
            // meshRenderer.material = tile.type switch
            // {
            //     TileType.Water => waterMaterial,
            //     TileType.Grass => grassMaterial,
            //     TileType.Mountain => mountainMaterial,
            //     _ => grassMaterial
            // };
            var gradientTime = (tile.heightLevel + 3.0f) / 6.0f;
            var heightColour = heightGradient.Evaluate(gradientTime);
            Debug.Log($"Tile {i}, Level: {tile.heightLevel}, Time: {gradientTime}, Color: {heightColour}");
            _materialPropertyBlock.SetColor(BaseColour, heightColour);
            meshRenderer.SetPropertyBlock(_materialPropertyBlock);
        }

        Debug.Log("Finished generating tile meshes.");
    }

    private Mesh CreateSingleTileMesh(HexSphereGenerator.HexTile tile)
    {
        var mesh = new Mesh
        {
            name = $"Tile_{tile.id}_Mesh"
        };

        var cornerCount = tile.cornerVertices.Count;
        if (cornerCount < 3) return mesh;
        var vertexCount = cornerCount + 1; // Corners + Center
        // var heightOffset = tile.centerPosition.normalized * tile.heightLevel * visualHeightScale;
        var centerHeightOffset = tile.centerPosition.normalized * tile.heightLevel * visualHeightScale;
        
        // --- Vertices ---
        var vertices = new List<Vector3>(vertexCount) { Vector3.zero + centerHeightOffset };
        // Add corner vertices relative to the center
        for (var i = 0; i < cornerCount; i++)
        {
            vertices.Add(tile.cornerVertices[i] - tile.centerPosition);
        }
        mesh.SetVertices(vertices);

        // --- Triangles ---
        // Create triangles fanning out from the center vertex (index 0)
        var triangles = new List<int>(cornerCount * 3);
        for (var i = 0; i < cornerCount; i++)
        {
            triangles.Add(0); // Center vertex index
            triangles.Add(i + 1); // Current corner vertex index
            triangles.Add((i + 1) % cornerCount + 1); // Next corner vertex index (wrapping around using modulo)
        }

        mesh.SetTriangles(triangles, 0); // Submesh 0

        // --- Normals ---
        // For now, use the tile's center normal for all vertices (faceted look)
        var normals = new List<Vector3>(vertexCount);
        var normal = tile.centerPosition.normalized;
        for (var i = 0; i < vertexCount; i++) normals.Add(normal);
        // mesh.SetNormals(normals);
        mesh.RecalculateNormals();
        
        // --- UVs (Basic Planar Mapping - adjust as needed) ---
        var uvs = new List<Vector2>(vertexCount) { new Vector2(0.5f, 0.5f) };
        var right = Vector3.Cross(normal, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(normal, Vector3.forward).normalized; // Fallback
        var up = Vector3.Cross(right, normal).normalized;
        
        for (var i = 0; i < cornerCount; i++)
        {
            // Project corner onto a plane relative to center - very basic
            var relativePos = vertices[i + 1] - centerHeightOffset;
            var u = Vector3.Dot(relativePos, right) * 0.5f + 0.5f; // Scale and center
            var v = Vector3.Dot(relativePos, up) * 0.5f + 0.5f; // Scale and center
            uvs.Add(new Vector2(u, v));
        }

        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds(); // Important for visibility culling

        return mesh;
    }
}